using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TeamWeeklyMeeting;

internal sealed class LoginCredential
{
    public string Id { get; set; } = "";
    public string Password { get; set; } = "";
    internal bool IsValid => Id!=null && Regex.IsMatch(Id,@"^[A-Za-z0-9_.@-]{1,100}$") && Password is {Length: >0 and <=4096};
}

internal sealed class AuthStore(string path)
{
    private static readonly byte[] Entropy="NETCUS.TeamWeeklyMeeting.AutoLogin.v1"u8.ToArray();
    internal string PathName => path;
    internal bool Exists => File.Exists(path);
    internal LoginCredential? Read()
    {
        if(!File.Exists(path))return null;
        if(new FileInfo(path).Length>65536)throw new InvalidDataException("Invalid credential file");
        byte[] clear=ProtectedData.Unprotect(File.ReadAllBytes(path),Entropy,DataProtectionScope.CurrentUser);
        try {
            var value=JsonSerializer.Deserialize<LoginCredential>(clear);
            if(value?.IsValid!=true)throw new InvalidDataException("Invalid credential data");
            return value;
        } finally { CryptographicOperations.ZeroMemory(clear); }
    }
    internal void Save(LoginCredential credential)
    {
        if(!credential.IsValid)throw new InvalidDataException("Invalid credential data");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        byte[] clear=JsonSerializer.SerializeToUtf8Bytes(credential);
        try {
            byte[] encrypted=ProtectedData.Protect(clear,Entropy,DataProtectionScope.CurrentUser);
            File.WriteAllBytes(path+".tmp",encrypted);File.Move(path+".tmp",path,true);
            var saved=Read();
            if(saved?.Id!=credential.Id||saved.Password!=credential.Password)throw new InvalidDataException("Credential verification failed");
        } finally { CryptographicOperations.ZeroMemory(clear); }
    }
    internal void Delete() { File.Delete(path);File.Delete(path+".tmp"); }
}
