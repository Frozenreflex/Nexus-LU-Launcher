using System;
using System.IO;
using System.Net;
using NLUL.Core.Client.Source;

namespace NLUL.Core.Client.Download
{
    public abstract class FileDownloadMethod : DownloadMethod
    {
        /// <summary>
        /// Creates the download method.
        /// </summary>
        /// <param name="systemInfo">Information about the system.</param>
        /// <param name="source">Source of the client.</param>
        public FileDownloadMethod(SystemInfo systemInfo, ClientSourceEntry source) : base(systemInfo, source)
        {
            
        }

        /// <summary>
        /// Download location of the client.
        /// </summary>
        public string DownloadLocation => Path.Combine(this.SystemInfo.SystemFileLocation, "client." + this.Source.Method.ToLower());
        
        /// <summary>
        /// Extract location of the client.
        /// </summary>
        public string ExtractLocation => Path.Combine(this.SystemInfo.SystemFileLocation, "ClientExtract");

        /// <summary>
        /// Size of the client to download. Intended to be set after
        /// a download starts.
        /// </summary>
        public override long ClientDownloadSize { get; protected set; }

        /// <summary>
        /// Size of the client that has been downloaded.
        /// </summary>
        public override long DownloadedClientSize => File.Exists(this.DownloadLocation) ? new FileInfo(this.DownloadLocation).Length : 0;

        /// <summary>
        /// Downloads the client.
        /// </summary>
        /// <param name="force">Whether to force download.</param>
        private void DownloadClient(bool force)
        {
            // Return if the client exists and isn't forced.
            if (!force && File.Exists(this.DownloadLocation))
            {
                return;
            }
            
            // Delete the existing download if it exists.
            if (File.Exists(this.DownloadLocation))
            {
                File.Delete(this.DownloadLocation);
            }
            
            // Download the client.
            Directory.CreateDirectory(Path.GetDirectoryName(this.SystemInfo.ClientLocation));
            var client = new WebClient();
            var fileStream = client.OpenRead(this.Source.Url);
            this.ClientDownloadSize = long.Parse(client.ResponseHeaders["Content-Length"]);
            using var targetFileStream = File.Create(this.DownloadLocation);
            fileStream?.CopyTo(targetFileStream);
        }
        
        /// <summary>
        /// Downloads and extracts the client.
        /// </summary>
        public override void Download()
        {
            // Download the client if not done already.
            this.OnDownloadStateChanged("Download");
            this.DownloadClient(false);
            
            try
            {
                // Try to extract the existing file.
                this.OnDownloadStateChanged("Extract");
                this.Extract();
            }
            catch (Exception)
            {
                // Re-download the client.
                this.OnDownloadStateChanged("Download");
                this.DownloadClient(true);
                this.OnDownloadStateChanged("Extract");
                this.Extract();
            }
            
            // Verify the client.
            this.OnDownloadStateChanged("Verify");
            if (!this.Verify())
            {
                this.OnDownloadStateChanged("VerifyFailed");
                return;
            }
            File.Delete(this.DownloadLocation);
        }

        /// <summary>
        /// Extracts the downloaded client.
        /// </summary>
        public abstract void Extract();
    }
}