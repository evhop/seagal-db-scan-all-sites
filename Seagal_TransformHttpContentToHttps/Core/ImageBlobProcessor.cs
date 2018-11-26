using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using WPDatabaseWork.Model;

namespace WPDatabaseWork.Core
{
    public class ImageBlobProcessor
    {
        private ILogger _logger { get; }
        public IContext _context { get; }

        public ImageBlobProcessor(IContext context)
        {
            _context = context;
        }

        public string Process(DateTime exportDateTime, List<View.File> dataFiles)
        {
            
            int count = 0;
            double processStart = 30;
            double processStop = 100;
            double processInterval = 10;

            Console.WriteLine($"Processing {count} of {dataFiles.Count} files to blobcontainer");
            string status = "";

            if (dataFiles.Count > 0)
            {
                var container = GetBlobContainer();
                foreach (var dataFile in dataFiles)
                {
                    ProcessFile(dataFile, container, exportDateTime);
                    count++;
                    if (count % processInterval == 0)
                    {
                        int processStep = (int)Math.Round((double)(100 * count) / dataFiles.Count);
                        processStart += processStep;
                        Console.WriteLine($"Processed {count} of {dataFiles.Count} files to blobcontainer");
                    }
                }
            }
            status = $"running - All {dataFiles.Count} files uploaded to the blob";
            return status;
            
            return "";
        }
        private void ProcessFile(View.File file, CloudBlobContainer container, DateTime exportDateTime)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(file.LocalPath);
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                Stream inputStream = response.GetResponseStream();

                var filePath = file.BlobPath;
                CloudBlockBlob blob = container.GetBlockBlobReference(filePath);
                if (filePath.Contains("150x150"))
                {
                    //ladda upp thumbnail
                    using (var image = new Bitmap(Image.FromStream(inputStream)))
                    {
                        Image.GetThumbnailImageAbort getThumbnailImageAbort = new Image.GetThumbnailImageAbort(ThumbnailCallback);
                        Image pThumbnail = image.GetThumbnailImage(150, 150, getThumbnailImageAbort, new IntPtr());

                        using (var memoryStream = new MemoryStream())
                        {
                            pThumbnail.Save(memoryStream, GetImageFormat(file.BlobPath));
                            blob.UploadFromStreamAsync(memoryStream).Wait();
                        }
                        return;
                    }
                }
                else
                {
                    blob.UploadFromStreamAsync(inputStream).Wait();
                }

                Console.WriteLine($"Uploaded file {file.LocalPath} to {file.BlobPath}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to upload image {file.LocalPath}. {e.Message}");
            }
        }
        
        private CloudBlobContainer GetBlobContainer()
        {
            var storageUri = new StorageUri(new Uri(_context.Settings.DestinationSite.GetAzureBlobUrl, UriKind.Absolute));
            var credentials = new StorageCredentials(_context.Settings.DestinationSite.GetAzureBlobAccount, _context.Settings.DestinationSite.DestinationAzureBlobKey);

            //DefaultEndpointsProtocol=https;AccountName=aomeassetsstage;AccountKey=DAfgOxgSqD1uZNSCCIHMA/ARRc/IZLmSlgpm9E870QZXvcp4FkyMInQxMm/J0oZINJaR8ZTmS/M/HtDTZsjtTQ==;EndpointSuffix=core.windows.net

            var client = new CloudBlobClient(storageUri, credentials);

            var container = client.GetContainerReference(_context.Settings.DestinationSite.GetBlobContainer());
            container.CreateIfNotExistsAsync().Wait();

            //Default inställning för en container är att det är privat, så vi måste sätta den till blob
            container.SetPermissionsAsync(
                new BlobContainerPermissions
                {
                    PublicAccess = BlobContainerPublicAccessType.Blob
                }).Wait();
            return container;
        }
        
        #region Helpers
        private ImageFormat GetImageFormat(string extension)
        {
            ImageFormat imageFormat = null;
            switch (extension)
            {
                case ".png":
                    imageFormat = ImageFormat.Png;
                    break;
                case ".gif":
                    imageFormat = ImageFormat.Gif;
                    break;
                case ".bmp":
                    imageFormat = ImageFormat.Bmp;
                    break;
                default:
                    imageFormat = ImageFormat.Jpeg;
                    break;
            }
            return imageFormat;
        }

        public bool ThumbnailCallback()
        {
            return true;
        }
        #endregion
    }
}

