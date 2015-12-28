using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace HeizungBackgroundApp
{
    internal static class FileHelper
    {
        public static async Task AppendAllTextAsync(StorageFolder folder, string fileName, string text)
        {
            byte[] encodedText = Encoding.Unicode.GetBytes(text);

            var file = await folder.CreateFileAsync(fileName, CreationCollisionOption.OpenIfExists);

            using (Stream stream = await file.OpenStreamForWriteAsync())
            {
                stream.Seek(0, SeekOrigin.End);
                await stream.WriteAsync(encodedText, 0, encodedText.Length);
            }
        }
    }
}
