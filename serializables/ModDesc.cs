using MelonLoader;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace VRCModNetwork.Serializables
{
    [Serializable]
    internal class ModDesc
    {
        public string name;
        public string version;
        public string author;
        public string downloadLink;

        [JsonProperty("type")]
        public string baseClass;

        public ModDesc(string name, string version, string author, string downloadLink, string baseClass)
        {
            this.name = name;
            this.version = version;
            this.author = author;
            this.downloadLink = downloadLink;
            this.baseClass = baseClass;
        }

        public bool Equals(ModDesc modDesc)
        {
            return name.Equals(modDesc.name) && version.Equals(modDesc.version) && baseClass.Equals(modDesc.baseClass);
        }


        public static List<ModDesc> GetAllMods()
        {
            List<ModDesc> list = new List<ModDesc>();
            // TODO
            //foreach (VRCMod mod in ModManager.Mods) list.Add(new ModDesc(mod.Name, mod.Version, mod.Author, mod.DownloadLink ?? "", "VRCMod"));
            //foreach (VRModule mod in ModManager.Modules) list.Add(new ModDesc(mod.Name, mod.Version, mod.Author, "", "VRModule"));
            List<MelonMod> melonmods = typeof(Main).GetField("Mods", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as List<MelonMod>;
            foreach (MelonMod mod in melonmods)
                list.Add(new ModDesc(mod.InfoAttribute.Name, mod.InfoAttribute.Version, mod.InfoAttribute.Author, mod.InfoAttribute.DownloadLink, "MelonMod"));
            return list;
        }

        public static string CreateModlistJson(List<ModDesc> modlist)
        {
            string modListString = "";
            bool firstdone = false;
            foreach (ModDesc mod in modlist)
            {
                if (firstdone) modListString += ",";
                else firstdone = true;

                modListString += "{\"name\":\"" + ParseJsonString(mod.name) + "\",\"version\":\"" + ParseJsonString(mod.version) + "\",\"author\":\"" + ParseJsonString(mod.author) + "\",\"downloadLink\":\"" + ParseJsonString(mod.downloadLink) + "\",\"type\":\"" + ParseJsonString(mod.baseClass) + "\"}";
            }
            return modListString;
        }

        private static string ParseJsonString(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }

}