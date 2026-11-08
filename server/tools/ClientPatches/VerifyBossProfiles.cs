// Offline x86 fixture for the owner's approved 6711 GameData/GraphicData builds.
// Runs only disposable copies; never loads into or modifies a running client.
using System;
using System.IO;
using System.ComponentModel;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

internal static class VerifyBossProfiles
{
    const string Work = @"C:\Users\Administrator\Desktop\Revora\Source\Revora-6711\obj\chi-refill-validation\boss-hud-space\native";
    static readonly string[] Names = { "Moonfang King", "Dawnfire King", "Sunscar Tyrant", "Nightveil King", "Misery Monk", "Awakened Flame", "Thrilling Spook", "Furious Flame", "Tyrant Nemesis", "HorrifyingSpecter", "TeratoDragon" };
    static readonly int[] Health = {100000000,100000000,100000000,100000000,1000000,3000000,6000000,9000000,12000000,5000000,50000000};
    static readonly string[] AbyssNames = {"Hill Spirit","Swift Devil","Vigor Banshee","Cleansing Devil","Cursed Andrew","Cursed Peter","Cursed Philip","Cursed Timothy","Cursed Daphne","Cursed Victoria","Cursed Wayne","Cursed Theodore","Sealed Satan","Beast Satan","Fury Satan"};
    static readonly int[] AbyssHealth = {50000,60000,60460,65535,65535,65535,65535,65535,65535,65535,65535,65535,100000,150000,200000};
    static readonly string[] WarriorNames = {"SealingStele","TinyMindofEvil","MatureMindofEvil","GiantMindofEvil"};
    static readonly int[] WarriorHealth = {30000,50000,100000,200000};
    static readonly string[] AbyssOriginals = {"HillSpirit","SwiftDevil","Banshee","CleansingDevil","Andrew","Peter","Philip","Timothy","Daphne","Victoria","Wayne","Theodore","Satan","BeastSatan","FurySatan"};
    static readonly Dictionary<string,string> Hashes = new Dictionary<string,string> {
        {"GameData.dll","8f5687dd11a17e7950358c19aefbb197e220f8f1fc24442a826ea0e28a2e409c"},
        {"GraphicData.dll","16cc9ef16175a917202c7437ebe1fe6615094e9c7c2c20668660d4015760c971"},
        {"TqPackage.dll","6458228d12cbc61eda3cf33b7a726f49d7e0c82f85e851672ac7d4b2154a7ace"},
        {"DataThread.dll","dbe2317e35733318390ea4972e38a974c7af5b2371e4f879ae00914385877769"},
        {"graphic.dll","6cb8ef6f276ab5c9516d91342a5ed6c717ab0a939e98dc2b3cab428683a376d2"},
        {"NdResUpdate.dll","15cd0af9094c52a13238a4c416047725eb21cab0b2b577ac6387471c81a0e25a"},
        {"Microsoft.VC90.CRT/Microsoft.VC90.CRT.manifest","0c838c4262f99f27495a7c2a1bf4ec8f482d1c9bc2493c3c19b9360f1a06b8eb"},
        {"Microsoft.VC90.CRT/msvcm90.dll","7a74da389fbd10a710c294c2e914dc6f18e05f028f07958a2fa53ac44f0e4b90"},
        {"Microsoft.VC90.CRT/msvcp90.dll","06918cf99ad26cd6cf106881c0d5bdb212dc0bac4549805c9f5906e3d03d152c"},
        {"Microsoft.VC90.CRT/msvcr90.dll","8e7fe1a1f3550c479ffd86a77bc9d10686d47f8727025bb891d8f4f0259354c8"},
        {"Microsoft.VC90.MFC/mfc90.dll","a1a6f3e18b097ab046e8771a01f8b727a44348b28b82b5beb15ee311ad27cdd6"},
        {"Microsoft.VC90.MFC/mfc90u.dll","0ea4cd410da764916ea201c0c1e16752e0d3dc9d8571510782af4aae62509af7"},
        {"Microsoft.VC90.MFC/mfcm90.dll","4e1455f9f205a501ad0079b4b918943a752cd3f3bfece8b36397e5700c9f09f1"},
        {"Microsoft.VC90.MFC/mfcm90u.dll","06d0444c7c7c84b9dfbb231245368f04529c505daefe4fb52cc459e487838008"},
        {"Microsoft.VC90.MFC/Microsoft.VC90.MFC.manifest","fe3f3fb142a2ed91c5fa5950002b616ed14ef853c739ac908c846c16179e34b2"}
    };
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr LoadLibraryEx(string path,IntPtr file,uint flags);
    [DllImport("kernel32.dll",CharSet=CharSet.Ansi,SetLastError=true)] static extern IntPtr GetProcAddress(IntPtr module,string name);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr Query();
    [UnmanagedFunctionPointer(CallingConvention.ThisCall)] delegate void LoadMonsters(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.ThisCall,CharSet=CharSet.Ansi)] delegate IntPtr Lookup(IntPtr self,string name);
    static void Verify(string file,string expected) {
        using(var stream=File.OpenRead(file)) using(var sha=SHA256.Create())
            if(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant()!=expected) throw new InvalidOperationException("Unexpected fixture input: "+Path.GetFileName(file));
    }
    static int Main(string[] args) {
        if(IntPtr.Size!=4 || args.Length!=1 || (args[0]!="before" && args[0]!="after" && args[0]!="abyss-before" && args[0]!="abyss-after" && args[0]!="warrior-before" && args[0]!="warrior-after" && args[0]!="ancient-before" && args[0]!="ancient-after")) return 2;
        bool abyss=args[0].StartsWith("abyss-",StringComparison.Ordinal),patched=args[0].EndsWith("after",StringComparison.Ordinal);
        bool ancient=args[0].StartsWith("ancient-",StringComparison.Ordinal);
        bool warrior=args[0].StartsWith("warrior-",StringComparison.Ordinal);
        string work=abyss || warrior || ancient?Work.Replace("boss-hud-space","abyss-boss-hud"):Work;
        string[] names=ancient?new[]{"AncientDevil","DevilGuard"}:warrior?WarriorNames:abyss?AbyssNames:Names;int[] health=ancient?new[]{65535,6590}:warrior?WarriorHealth:abyss?AbyssHealth:Health;
        Directory.SetCurrentDirectory(work);
        foreach(var pair in Hashes) Verify(Path.Combine(work,pair.Key),pair.Value);
        string catalog=ancient?(patched?"fe70117a12f17b9b2b27be9c0fb3149302372acf7f213a0a2a1b800090cb2c92":"99fa2c8f14c3851d797a0b20fbd325053f9acee35c7dab4c5e73edc2f5792585"):abyss || warrior?(patched?"df6431be254159ce83a812532b6f24330df799bfec66e1cfc2ba6436b5d6970a":"99fa2c8f14c3851d797a0b20fbd325053f9acee35c7dab4c5e73edc2f5792585")
            :patched?"99fa2c8f14c3851d797a0b20fbd325053f9acee35c7dab4c5e73edc2f5792585":"bc40ae4d515ffc3475da17a6630e1155ef59736116cb6bc40185178e5c2f68c0";
        Verify(Path.Combine(work,"ini/monster.dat"),catalog);
        IntPtr module=LoadLibraryEx(Path.Combine(work,"GameData.dll"),IntPtr.Zero,0x100|0x800);
        if(module==IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        var query=(Query)Marshal.GetDelegateForFunctionPointer(GetProcAddress(module,"RoleDataQuery"),typeof(Query));
        IntPtr role=query();if(role==IntPtr.Zero) throw new InvalidOperationException("No role data");
        var load=(LoadMonsters)Marshal.GetDelegateForFunctionPointer(IntPtr.Add(module,0x173b9),typeof(LoadMonsters));
        load(role);
        var lookup=(Lookup)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(role),0x2c),typeof(Lookup));
        for(int i=0;i<names.Length;i++) {
            IntPtr profile=lookup(role,names[i]);
            if(!patched && (ancient?i==1:!warrior && (abyss || i<9))) {
                if(profile!=IntPtr.Zero) throw new InvalidOperationException("Baseline unexpectedly resolves "+names[i]);
                Console.WriteLine("PASS: baseline native lookup fails for "+names[i]);
            } else {
                if(profile==IntPtr.Zero) throw new InvalidOperationException("Missing native profile: "+names[i]);
                int hp=Marshal.ReadInt32(profile,0x10);byte boss=Marshal.ReadByte(profile,0x10c);
                int expectedBoss=ancient?(i==0 && patched?1:0):warrior && i==0?0:1;
                if(hp!=health[i] || boss!=expectedBoss) throw new InvalidOperationException(names[i]+" HP="+hp+" Boss="+boss);
                Console.WriteLine("PASS: native "+names[i]+" MaxLife="+hp+" Boss="+boss);
            }
        }
        if(abyss) for(int i=0;i<AbyssOriginals.Length;i++) {
            IntPtr profile=lookup(role,AbyssOriginals[i]);
            int hp=i==0?50000:i==1?55000:i==2?60460:65535;
            if(profile==IntPtr.Zero || Marshal.ReadInt32(profile,0x10)!=hp || Marshal.ReadByte(profile,0x10c)!=0)
                throw new InvalidOperationException("Legacy Abyss profile changed: "+AbyssOriginals[i]);
            Console.WriteLine("PASS: native legacy "+AbyssOriginals[i]+" preserved MaxLife="+hp+" Boss=0");
        }
        return 0;
    }
}
