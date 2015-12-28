using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Windows.Storage;

namespace HeizungBackgroundApp
{
    public sealed class OnlineCheckConfig
    {
        internal static async Task<OnlineCheckConfig> LoadAsync(StorageFolder folder, string fileName)
        {
            //var c = new OnlineCheckConfig();
            //await c.Save(folder, fileName);

            var file = await folder.GetFileAsync(fileName);
            using (var stream = await file.OpenAsync(FileAccessMode.Read))
            {
                var ser = new DataContractSerializer(typeof(OnlineCheckConfig));
                //var ser = new XmlSerializer(typeof(OnlineCheckConfig));
                using (var str = stream.AsStreamForRead())
                {
                    //var cfg = ser.Deserialize(str) as OnlineCheckConfig;
                    var cfg = ser.ReadObject(str) as OnlineCheckConfig;
                    return cfg;
                }
            }
        }

        internal async Task Save(StorageFolder folder, string fileName)
        {
            var file = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
            using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                //var ser = new XmlSerializer(typeof(OnlineCheckConfig));
                var ser = new DataContractSerializer(typeof(OnlineCheckConfig));
                using (var str = stream.AsStreamForWrite())
                {
                    //    ser.Serialize(str, this);
                    ser.WriteObject(str, this);
                }
            }
        }


        List<OnlineCheckConfigEntry> _Entries = new List<OnlineCheckConfigEntry>();
        public IList<OnlineCheckConfigEntry> Entries
        {
            get { return _Entries; }
            //set { _Entries = value; }
        }

    }

    public sealed class OnlineCheckConfigEntry
    {
        public string Url { get; set; }
        public int WaitTimeInSec { get; set; }

        public string Folder { get; set; }

        public string FileNamePattern { get; set; }

        public string LogPattern { get; set; }
    }
}
