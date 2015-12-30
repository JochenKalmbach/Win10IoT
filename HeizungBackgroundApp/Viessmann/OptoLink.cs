﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.SerialCommunication;
using Windows.Storage;
using Windows.Storage.Streams;

namespace HeizungBackgroundApp.Viessmann
{
    public sealed class OptoLink
    {
        public OptoLink(string device)
        {
            _Device = device;
        }

        private ILog _Logger = Logging.GetLogger(typeof(OptoLink));

        private string _Device;

        internal async Task DoWorkAsync(CancellationToken cancel)
        {
            var cfg = await LoadConfigAsync();

            var sw = Stopwatch.StartNew();
            var swInner = Stopwatch.StartNew();
            //await WaitForFirst
            while (cancel.IsCancellationRequested == false)
            {
                try
                {
                    using (var port = await OpenPortAsync(_Device))
                    {
                        port.ReadTimeout = new TimeSpan(0, 0, 0, 0, 500);

                        while (cancel.IsCancellationRequested == false)
                        {
                            await DiscardInputStreamAsync(port);

                            await WaitFor0x05Async(port, cancel);

                            // Reduce now read timeout
                            port.ReadTimeout = new TimeSpan(0, 0, 0, 0, 500);  // 500 ms

                            sw.Restart();
                            swInner.Restart();
                            DateTime dt = DateTime.Now;
                            var sb = new StringBuilder();
                            sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0:HH:mm:ss}", dt);
                            bool doBreak = false;
                            int reqCnt = 0;
                            foreach (OptoLinkConfigEntry ce in cfg.Values)
                            {
                                reqCnt++;
                                sb.Append("\t");
                                var readData = await ReadDataAsync(port, ce.AddressAsInt, ce.Len);
                                string val = ce.GetDataString(readData);
                                sb.Append(ce.GetDataString(readData));

                                // Info: Do not wait for the sending of the e-mail...
                                Task t = CheckAlarm(cfg, ce, val);

                                // When using the KW protocol, we must not wait longer than about 750-950 ms... then we must first wait for the next 0x05...
                                if ((reqCnt > cfg.Max0x05Requests) || (swInner.ElapsedMilliseconds > Math.Min(750, cfg.Max0x05TimeMilliseconds)))
                                {
                                    _Logger.Debug("... interrupting: waiting for next 0x05...");
                                    await WaitFor0x05Async(port, cancel);
                                    swInner.Restart();
                                    reqCnt = 0;
                                }
                                if (sw.ElapsedMilliseconds > 10000)
                                {
                                    // if the delay was too much, then discad this data...
                                    _Logger.Debug(string.Format("Delay was too much ({0} ms)!", sw.ElapsedMilliseconds));
                                    doBreak = true;
                                    break;
                                }
                            }
                            if (doBreak)
                            {
                                continue;
                            }
                            sb.AppendLine();
                            _Logger.Debug(sb.ToString());

                            // Log into file...
                            var fld = await GetFolderAsync(dt, cfg.Folder);
                            string fn = string.Format(cfg.FileNamePattern, dt);
                            // Create file with headlines, if not yet existing
                            if (System.IO.File.Exists(System.IO.Path.Combine(fld.Path, fn)) == false)
                            {
                                var sb2 = new StringBuilder();
                                sb2.AppendFormat("DateTime");
                                foreach (var ce in cfg.Values)
                                {
                                    sb2.Append("\t");
                                    if (string.IsNullOrEmpty(ce.Text))
                                        sb2.Append("0x" + ce.AddressAsInt.ToString("X"));
                                    else
                                        sb2.Append(ce.Text.Replace('\t', '?'));
                                }
                                sb2.AppendLine();
                                await FileHelper.AppendAllTextAsync(fld, fn, sb2.ToString());
                            }
                            await FileHelper.AppendAllTextAsync(fld, fn, sb.ToString());

                            // Try to almost exactly match the _WaitTime
                            int waitTime = cfg.IntervallInSec * 1000 - (int)sw.ElapsedMilliseconds;
                            if (waitTime < 500)
                                waitTime = cfg.IntervallInSec * 1000;
                            await Task.Delay(waitTime);
                        }
                    }
                }
                catch (Exception exp)
                {
                    _Logger.Error(exp);
                    await Task.Delay(10000);
                }
            }
        }

        private Dictionary<OptoLinkConfigEntry, string> _ActiveAlarmEntries = new Dictionary<OptoLinkConfigEntry, string>();
        private async Task CheckAlarm(OptoLinkConfig cfg, OptoLinkConfigEntry ce, string val)
        {
            if (cfg.AlarmSmtp == null)
            {
                return;
            }
            string errText;
            if (ce.IsAlarmActive(val, out errText))
            {
                if (_ActiveAlarmEntries.ContainsKey(ce) == false)
                {
                    // Add to active list
                    _ActiveAlarmEntries.Add(ce, errText);
                    // Send E-Mail...
                    var client = new LightBuzz.SMTP.EmailClient
                    {
                        Server = cfg.AlarmSmtp.Server,
                        Port = cfg.AlarmSmtp.Port,
                        Username = cfg.AlarmSmtp.Username,
                        Password = cfg.AlarmSmtp.Password,
                        From = new LightBuzz.SMTP.MailBox(cfg.AlarmSmtp.From, cfg.AlarmSmtp.From),
                        To = new LightBuzz.SMTP.MailBox(cfg.AlarmSmtp.To, cfg.AlarmSmtp.To),
                        SSL = cfg.AlarmSmtp.Ssl,
                        Subject = "Comming: " + cfg.AlarmSmtp.Subject,
                        Message = errText
                    };
                    await client.SendAsync();
                }
            }
            else
            {
                if (_ActiveAlarmEntries.ContainsKey(ce))
                {
                    // get the old error text...
                    errText = _ActiveAlarmEntries[ce];
                    // Remove from active list
                    _ActiveAlarmEntries.Remove(ce);
                    // Send E-Mail...
                    var client = new LightBuzz.SMTP.EmailClient
                    {
                        Server = cfg.AlarmSmtp.Server,
                        Port = cfg.AlarmSmtp.Port,
                        Username = cfg.AlarmSmtp.Username,
                        Password = cfg.AlarmSmtp.Password,
                        From = new LightBuzz.SMTP.MailBox(cfg.AlarmSmtp.From, cfg.AlarmSmtp.From),
                        To = new LightBuzz.SMTP.MailBox(cfg.AlarmSmtp.To, cfg.AlarmSmtp.To),
                        SSL = cfg.AlarmSmtp.Ssl,
                        Subject = "Going: " + cfg.AlarmSmtp.Subject,
                        Message = errText
                    };
                    await client.SendAsync();
                }
            }
        }

        private async Task DiscardInputStreamAsync(SerialDevice port)
        {
            var b = new byte[100].AsBuffer();
            var b2 = await port.InputStream.ReadAsync(b, 100, InputStreamOptions.Partial);
        }

        private async Task<byte[]> ReadDataAsync(SerialDevice port, int addr, int len)
        {
            byte[] data = new byte[] { 0xF7, 0x00, 0x00, 0x10 };
            data[1] = (byte)((addr >> 8) & 0xFF);
            data[2] = (byte)((addr) & 0xFF);
            data[3] = (byte)((len) & 0xFF);
            await port.OutputStream.WriteAsync(data.AsBuffer());
            //await port.OutputStream.FlushAsync();  // NotSuportedException

            var s = string.Join(" ", data.Select(p => string.Format("0x{0}", p.ToString("X"))));
            _Logger.Debug(string.Format("Send: {0}", s));

            // Now read the data:
            byte[] readData = new byte[len];
            await port.InputStream.ReadAsync(readData.AsBuffer(), (uint) len, InputStreamOptions.None);

            s = string.Join(" ", readData.Select(p => string.Format("0x{0}", p.ToString("X"))));
            _Logger.Debug(string.Format("Recv: {0} ({1})", s, ToDec(readData)));
            return readData;
        }

        decimal ToDec(byte[] data)
        {
            int i = data[0];
            for (int idx = 1; idx < data.Length; idx++)
                i |= (data[idx]) << (idx * 8);
            return ((decimal)i) / 10;
        }

        private async Task WaitFor0x05Async(SerialDevice port, CancellationToken cancel)
        {
            var buffer = new Windows.Storage.Streams.Buffer(1);
            try
            {
                while (cancel.IsCancellationRequested == false)
                {
                    var buf = await port.InputStream.ReadAsync(buffer, 1, InputStreamOptions.None);
                    if (buf.Length == 1 && buf.ToArray()[0] == 0x05)
                    {
                        // acknowledge this by sending 0x01
                        byte[] ack = new byte[1] { 0x01 };
                        await port.OutputStream.WriteAsync(ack.AsBuffer());
                        //await port.OutputStream.FlushAsync();  // Method not implemented ;)
                        return;
                    }
                    await Task.Delay(1000);
                }
            }
            catch(Exception exp)
            {
                _Logger.Error(exp);
            }
        }

        //private async Task WaitForProtocol300Async(SerialDevice port, CancellationToken cancel)
        //{
        //    try
        //    {
        //        // Discrad any data in the queu...
        //        await DiscardInputStreamAsync(port);

        //        // First send "0x04" to reset to normal communication
        //        byte[] resetCom = new byte[] { 0x01 };
        //        await port.OutputStream.WriteAsync(resetCom.AsBuffer());
        //        while(cancel.IsCancellationRequested == false)
        //        {
        //            // Wait for 0x05
        //            var buffer = new Windows.Storage.Streams.Buffer(1);
        //            var buf = await port.InputStream.ReadAsync(buffer, 1, InputStreamOptions.None);
        //            if (buf.Length == 1 && buf.ToArray()[0] == 0x05)
        //            {
        //                // acknowledge this by sending 0x01
        //                byte[] ack = new byte[] { 0x16, 0x00, 0x00 };
        //                await port.OutputStream.WriteAsync(ack.AsBuffer());

        //                var buffer2 = new Windows.Storage.Streams.Buffer(1);
        //                var buf2 = await port.InputStream.ReadAsync(buffer2, 1, InputStreamOptions.None);
        //                if (buf2.Length == 1 && buf.ToArray()[0] == 0x06)
        //                {

        //                }


        //            }
        //            await Task.Delay(1000);
        //        }
        //    }
        //    catch (Exception exp)
        //    {
        //        _Logger.Error(exp);
        //    }
        //}

        private async Task<SerialDevice> OpenPortAsync(string id)
        {
            SerialDevice dev = await SerialDevice.FromIdAsync(id);
            dev.BaudRate = 4800;
            dev.Parity = SerialParity.Even;
            dev.DataBits = 8;
            dev.StopBits = SerialStopBitCount.Two;
            dev.ReadTimeout = new TimeSpan(0, 0, 5);
            dev.WriteTimeout = new TimeSpan(0, 0, 1);
            return dev;
        }

        private async Task<OptoLinkConfig> LoadConfigAsync()
        {
            var folder = await GetFolderAsync(null, null);
            var cfg = await OptoLinkConfig.LoadAsync(folder, "Heizung.config.json");
            return cfg;
        }

        private async Task<StorageFolder> GetFolderAsync(DateTime? dt, string folder)
        {
            var fld = await ApplicationData.Current.LocalFolder.CreateFolderAsync("Heizung", CreationCollisionOption.OpenIfExists);
            if (string.IsNullOrEmpty(folder) == false)
            {
                string f = folder;
                if (dt != null)
                {
                    f = string.Format(folder, dt.Value);
                }
                return await fld.CreateFolderAsync(f, CreationCollisionOption.OpenIfExists);
            }
            return fld;
        }

    }
}
