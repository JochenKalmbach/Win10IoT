using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Storage;

namespace HeizungBackgroundApp.Viessmann
{
    public sealed class OptoLinkConfig
    {
        public OptoLinkConfig()
        {
            IntervallInSec = 10;
        }

        internal static async Task<OptoLinkConfig> LoadAsync(StorageFolder folder, string fileName)
        {
            //var c = new OptoLinkConfig();
            //c.Values.Add(new OptoLinkConfigEntry { Text = "aaa", Address = "0x11" });
            //await c.Save(folder, fileName);

            var file = await folder.GetFileAsync(fileName);
            using (var stream = await file.OpenAsync(FileAccessMode.Read))
            {
                //var ser = new XmlSerializer(typeof(OptoLinkConfig));
                //var ser = new DataContractSerializer(typeof(OptoLinkConfig));
                var ser = new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(OptoLinkConfig));
                using (var str = stream.AsStreamForRead())
                {
                    var cfg = ser.ReadObject(str) as OptoLinkConfig;
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
                //var ser = new DataContractSerializer(typeof(OptoLinkConfig));
                var ser = new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(OptoLinkConfig));
                using (var str = stream.AsStreamForWrite())
                {
                    //    ser.Serialize(str, this);
                    ser.WriteObject(str, this);
                }
            }
        }


        List<OptoLinkConfigEntry> _Values = new List<OptoLinkConfigEntry>();
        public IList<OptoLinkConfigEntry> Values
        {
            get { return _Values; }
        }

        public string Device { get; set; }

        public int IntervallInSec { get; set; }

        public string Folder { get; set; }

        public string FileNamePattern { get; set; }
    }

    public sealed class OptoLinkConfigEntry
    {
        public OptoLinkConfigEntry()
        {
            Len = 2;
            DecPlaces = 1;
        }

        public string Text { get; set; }

        public string Address
        {
            get { return AddressAsInt.ToString(System.Globalization.CultureInfo.InvariantCulture); }
            set
            {
                if (value.StartsWith("0x", StringComparison.Ordinal))
                    AddressAsInt = int.Parse(value.Substring(2), NumberStyles.HexNumber);
                else
                    AddressAsInt = int.Parse(value, NumberStyles.Any);
            }
        }

        internal int AddressAsInt { get; set; }

        public int Len { get; set; }

        public int DecPlaces { get; set; }

        public string Format { get; set; }

        private decimal? _Factor;
        private decimal Factor
        {
            get
            {
                if (_Factor.HasValue)
                    return _Factor.Value;
                _Factor = (decimal)Math.Pow(10, DecPlaces);
                return _Factor.Value;
            }
        }

        public string GetDataString([ReadOnlyArray] byte[] readData)
        {
            if (readData.Length != Len)
                throw new ArgumentException("Invalid length", "readData");

            // Try to convert the data into a number
            int i = readData[0];
            for (int idx = 1; idx < readData.Length; idx++)
                i |= (readData[idx]) << (idx * 8);

            decimal dec = i;

            if (string.IsNullOrEmpty(Format))
            {
                if (DecPlaces != 0)
                    dec /= Factor;

                if (DecPlaces == 1)
                {
                    // Negative-Values:
                    // Try to convert bad data to good (65553.5)
                    if ((dec > 6400) && (dec < 6553.6M))
                        dec = dec - 6553.5M;
                }

                return dec.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            if (string.Equals(Format, "hhmmss", StringComparison.OrdinalIgnoreCase))
            {
                // Wandle die Zahl nach HH:mm:ss um:
                long val = (long)dec;
                val *= 10 * 1000 * 1000;  // convert to 100 ns
                var ts = new TimeSpan(val);
                return string.Format("{0,4}:{1}:{2}",
                  (ts.Hours + (ts.Days * 24)).ToString("0", CultureInfo.InvariantCulture),
                  ts.Minutes.ToString("00", CultureInfo.InvariantCulture),
                  ts.Seconds.ToString("00", CultureInfo.InvariantCulture)
                  );
            }
            return dec.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        // Chart Infos:

        public string Chart { get; set; }

        public string ChartY { get; set; }
    }
}
