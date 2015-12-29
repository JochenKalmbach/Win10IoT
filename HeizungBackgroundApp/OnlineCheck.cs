using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace HeizungBackgroundApp
{
    internal class OnlineCheck
    {
        private ILog _Logger = Logging.GetLogger(typeof(OnlineCheck));

        private async Task<OnlineCheckConfig> LoadConfigAsync()
        {
            var folder = await GetFolderAsync(null, null);
            var cfg = await OnlineCheckConfig.LoadAsync(folder, "OnlineCheck.config.xml");
            return cfg;
        }

        public async Task DoWorkAsync(CancellationToken cancel)
        {
            var cfg = await LoadConfigAsync();

            var entries = new List<Task>();
            foreach(var entry in cfg.Entries)
            {
                entries.Add(DoWorkAsync(cancel, entry));
            }

            Task.WaitAll(entries.ToArray());
        }

        private async Task DoWorkAsync(CancellationToken cancel, OnlineCheckConfigEntry checkEntry)
        { 
            var sw = Stopwatch.StartNew();
            while (cancel.IsCancellationRequested == false)
            {
                sw.Restart();
                if (await IsOnlineAsync(checkEntry))
                {
                    var dt = DateTime.Now;
                    string text = string.Format(checkEntry.LogPattern, dt) + Environment.NewLine;
                    var fld = await GetFolderAsync(dt, checkEntry.Folder);
                    await FileHelper.AppendAllTextAsync(fld, GetFileName(dt, checkEntry), text);
                }
                // Try to almost exactly match the _WaitTime
                int waitTime = checkEntry.WaitTimeInSec*1000 - (int) sw.ElapsedMilliseconds;
                if (waitTime < 1000)
                    waitTime = checkEntry.WaitTimeInSec * 1000;
                await Task.Delay(waitTime);
            }
        }

        private async Task<StorageFolder> GetFolderAsync(DateTime? dt, string folder)
        {
            var fld = await ApplicationData.Current.LocalFolder.CreateFolderAsync("OnlineCheck", Windows.Storage.CreationCollisionOption.OpenIfExists);
            if (string.IsNullOrEmpty(folder) == false)
            {
                string f = folder;
                if (dt != null)
                {
                    f = string.Format(folder, dt.Value);
                }
                return await fld.CreateFolderAsync(f, Windows.Storage.CreationCollisionOption.OpenIfExists);
            }
            return fld;
        }

        private string GetFileName(DateTime dt, OnlineCheckConfigEntry checkEntry)
        {
            return string.Format(checkEntry.FileNamePattern, dt);
        }

        private async Task<bool> IsOnlineAsync(OnlineCheckConfigEntry checkEntry)
        {
            try
            {
                using (var http = new Windows.Web.Http.HttpClient())
                {
                    using (var res = await http.GetAsync(new Uri(checkEntry.Url)))
                    {
                        res.Dispose();
                        http.Dispose();
                        _Logger.Debug(string.Format("Online: {0}", checkEntry.Url));
                        return true;
                    }
                }
            }
            catch (Exception exp)
            {
                _Logger.Debug(string.Format("### Exception ({0}): {1}", checkEntry.Url, exp));
            }
            return false;
        }

    }
}
