namespace TeamWeeklyMeeting;

internal sealed partial class MeetingWindow
{
    // SVG annotations live in the report document so scrolling/zooming keeps them aligned.
    private const string AnnotationInput = """
        (() => {
          let active=false, mode='move', color='#dc2626', width=4, scale=1;
          let svg, tools, pointer=null, current=null, strokes=[], history=[];
          const ns='http://www.w3.org/2000/svg';
          const log=action=>chrome.webview.postMessage({kind:'meeting-annotation',action,mode,count:strokes.length});
          const checkpoint=()=>{history.push(structuredClone(strokes));if(history.length>30)history.shift()};
          const render=()=>{
            svg.replaceChildren();
            for(const stroke of strokes){
              const path=document.createElementNS(ns,'path');path.dataset.ink='';
              path.setAttribute('d',stroke.points.map((p,i)=>(i?'L':'M')+p[0]+','+p[1]).join(' '));
              path.setAttribute('fill','none');path.setAttribute('stroke',stroke.color);
              path.setAttribute('stroke-width',stroke.width);path.setAttribute('opacity',stroke.opacity);
              path.setAttribute('stroke-linecap','round');path.setAttribute('stroke-linejoin','round');
              svg.append(path);
            }
          };
          const stop=()=>{
            if(pointer===null)return;
            const previous=pointer;pointer=null;current=null;
            try{document.documentElement.releasePointerCapture(previous)}catch{}
            log(mode==='erase'?'erase':'stroke');
          };
          const setMode=value=>{
            stop();mode=value;
            for(const button of tools.querySelectorAll('[data-mode]'))button.setAttribute('aria-pressed',String(button.dataset.mode===mode));
            document.documentElement.style.cursor=active&&mode!=='move'?'crosshair':'';
            log('mode');
          };
          const layout=()=>{
            if(!tools)return;
            tools.style.transform=`translateX(-50%) scale(${scale})`;
            tools.style.bottom=(12*scale)+'px';tools.style.maxWidth=Math.max(180,innerWidth/scale-24)+'px';
          };
          const undo=()=>{stop();if(history.length){strokes=history.pop();render();log('undo')}};
          const erase=(x,y)=>{
            const distance=(p,a,b)=>{
              const dx=b[0]-a[0],dy=b[1]-a[1],length=dx*dx+dy*dy;
              const t=length?Math.max(0,Math.min(1,((p[0]-a[0])*dx+(p[1]-a[1])*dy)/length)):0;
              return Math.hypot(p[0]-a[0]-t*dx,p[1]-a[1]-t*dy);
            };
            strokes=strokes.filter(s=>!s.points.some((p,i)=>distance([x,y],s.points[Math.max(0,i-1)],p)<=12+s.width/2));
            render();
          };
          const initialize=()=>{
            const viewport=document.getElementById('meeting-report-viewport');if(!viewport)return false;
            if(svg)return true;
            viewport.style.position='relative';
            svg=document.createElementNS(ns,'svg');svg.id='meeting-ink';svg.setAttribute('aria-hidden','true');
            svg.style.cssText='position:absolute;inset:0;width:100%;height:100%;overflow:visible;pointer-events:none;z-index:2147483600;display:none';
            tools=document.createElement('div');tools.id='meeting-drawing-tools';tools.dataset.meetingTools='';
            tools.setAttribute('role','toolbar');tools.setAttribute('aria-label','발표 그리기 도구');
            tools.title='발표용 임시 메모입니다. 보고서 새로고침 또는 앱 종료 시 사라집니다.';
            const style=document.createElement('style');
            style.textContent=`
              #meeting-drawing-tools{position:fixed;left:50%;width:max-content;z-index:2147483646;transform-origin:bottom center;display:none;align-items:center;justify-content:center;gap:5px;flex-wrap:wrap;padding:7px;background:#fff;border:1px solid #cbd0d7;border-radius:10px;box-shadow:0 4px 20px #0003;font:13px 'Malgun Gothic',sans-serif;color:#25282e;box-sizing:border-box}
              #meeting-drawing-tools button{display:inline-flex!important;align-items:center;justify-content:center;min-height:32px;padding:4px 9px;background:#fff;border:1px solid #d9dde2;border-radius:5px;color:#25282e;font:inherit;cursor:pointer;white-space:nowrap}
              #meeting-drawing-tools button:hover{background:#fff0e3}
              #meeting-drawing-tools button[aria-pressed=true]{background:#fff0e3;border-color:#d36b0a;color:#8a3e00}
              #meeting-drawing-tools button:focus-visible,#meeting-drawing-tools select:focus-visible{outline:2px solid #d36b0a;outline-offset:2px}
              #meeting-drawing-tools button[data-color]{min-width:28px;padding:4px;border-radius:50%}
              #meeting-drawing-tools select{height:32px;border:1px solid #d9dde2;border-radius:5px;background:white;color:#25282e;font:inherit}
              #meeting-drawing-tools .ink-options{display:flex;gap:5px;align-items:center;flex-wrap:wrap;justify-content:center}
            `;
            document.head.append(style);
            const options=document.createElement('span');options.className='ink-options';
            function button(label,title,click,parent=options){
              const node=document.createElement('button');node.type='button';node.dataset.meetingTool='';
              node.textContent=label;node.title=title;node.addEventListener('click',click);parent.append(node);return node;
            }
            const toggle=button('도구 접기','그리기 도구 접기/펼치기',event=>{
              const hidden=options.style.display==='none';options.style.display=hidden?'flex':'none';
              event.currentTarget.textContent=hidden?'도구 접기':'그리기 도구';
            },tools);
            toggle.dataset.action='toggle';
            for(const [value,label,key] of [['move','이동','M'],['pen','펜','D'],['highlight','형광펜','H'],['erase','지우개','E']]){
              const node=button(label,label+' ('+key+')'+(value==='erase'?' · 획 단위 삭제':''),()=>setMode(value));
              node.dataset.mode=value;node.setAttribute('aria-pressed',String(value===mode));
            }
            for(const [value,label] of [['#dc2626','빨강'],['#2563eb','파랑'],['#f59e0b','주황']]){
              const node=button('●',label,()=>{
                color=value;for(const item of tools.querySelectorAll('[data-color]'))item.setAttribute('aria-pressed',String(item.dataset.color===color));
              });node.dataset.color=value;node.style.color=value;node.setAttribute('aria-label',label);node.setAttribute('aria-pressed',String(value===color));
            }
            const sizes=document.createElement('select');sizes.setAttribute('aria-label','선 굵기');
            for(const [value,label] of [[2,'얇게'],[4,'보통'],[7,'굵게']]){const option=new Option(label,String(value));option.selected=value===width;sizes.add(option)}
            sizes.addEventListener('change',()=>width=Number(sizes.value));options.append(sizes);
            button('되돌리기','되돌리기 (Ctrl+Z) · 최대 30회',undo).dataset.action='undo';
            button('전체 지우기','현재 발표자의 그림 모두 지우기',()=>{stop();if(strokes.length){checkpoint();strokes=[];render();log('clear')}}).dataset.action='clear';
            tools.append(options);viewport.append(svg,tools);layout();return true;
          };
          window.__meetingDrawingActive=()=>active&&mode!=='move';
          window.__meetingAnnotationScale=value=>{scale=value;layout()};
          window.__meetingAnnotations=(enabled,value=1)=>{
            stop();active=enabled;scale=value;
            if(enabled&&!initialize())return;
            if(svg){svg.style.display=enabled?'block':'none';tools.style.display=enabled?'flex':'none';layout();setMode('move')}
          };
          document.addEventListener('pointerdown',e=>{
            if(!active||mode==='move'||e.button!==0||e.target.closest('[data-meeting-tools]')||e.clientX>=document.documentElement.clientWidth)return;
            e.preventDefault();e.stopImmediatePropagation();checkpoint();pointer=e.pointerId;
            try{document.documentElement.setPointerCapture(pointer)}catch{}
            if(mode==='erase'){erase(e.pageX,e.pageY);return}
            current={color,width:mode==='highlight'?width*4:width,opacity:mode==='highlight'?0.3:1,points:[[e.pageX,e.pageY],[e.pageX+.1,e.pageY+.1]]};
            strokes.push(current);render();
          },true);
          document.addEventListener('pointermove',e=>{
            if(pointer!==e.pointerId)return;
            e.preventDefault();e.stopImmediatePropagation();
            if(mode==='erase')erase(e.pageX,e.pageY);
            else if(current){current.points.push([e.pageX,e.pageY]);render()}
          },true);
          for(const name of ['pointerup','pointercancel','lostpointercapture'])document.addEventListener(name,stop,true);
          document.addEventListener('keydown',e=>{
            if(!active||e.altKey||e.metaKey||e.target.closest('select,input,textarea'))return;
            if(e.ctrlKey&&e.key.toLowerCase()==='z'){e.preventDefault();undo();return}
            if(e.ctrlKey)return;
            const value={m:'move',d:'pen',h:'highlight',e:'erase'}[e.key.toLowerCase()];
            if(value){e.preventDefault();setMode(value)}
          },true);
          addEventListener('resize',layout);
        })()
        """;
}
