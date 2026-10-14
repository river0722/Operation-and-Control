using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using Operations_Control.Models;

namespace Operations_Control.Files
{
    public static class DeviceConfigurationStore
    {
        public static string DirectoryPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PMAC Operations Control"); } }
        public static string ProfilePath(bool simulation) { return Path.Combine(DirectoryPath, simulation ? "simulation.xml" : "hardware.xml"); }
        public static void Save(string path, DeviceConfiguration configuration)
        {
            configuration.Validate(); Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temporary = path + ".tmp";
            using (StreamWriter writer = new StreamWriter(temporary, false, new System.Text.UTF8Encoding(false))) new XmlSerializer(typeof(DeviceConfiguration)).Serialize(writer, configuration);
            if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
        }
        public static DeviceConfiguration Load(string path)
        {
            XmlReaderSettings settings = new XmlReaderSettings { ProhibitDtd = true, XmlResolver = null };
            using (XmlReader reader = XmlReader.Create(path, settings))
            {
                DeviceConfiguration configuration = (DeviceConfiguration)new XmlSerializer(typeof(DeviceConfiguration)).Deserialize(reader);
                configuration.Validate(); return configuration;
            }
        }
    }
}
