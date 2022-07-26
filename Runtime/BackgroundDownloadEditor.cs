#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using UnityEngine;

namespace Unity.Networking
{
    class BackgroundDownloadEditor : BackgroundDownload
    {
        //可写目录
        public static string DataPath = Directory.GetCurrentDirectory();


        long _totalLength;
        long _downloadedByteCount;
        long _startDownloadPos;
        private Thread _childThread;

        public BackgroundDownloadEditor(BackgroundDownloadConfig config)
            : base(config)
        {
            //_status = BackgroundDownloadStatus.Failed;
            //_error = "Not implemented for Unity Editor";
            _status = BackgroundDownloadStatus.Downloading;

            HttpWebRequest request = HttpWebRequest.CreateHttp(config.url);
            request.Method = "HEAD";
            using (HttpWebResponse httpWebResponse = (HttpWebResponse)request.GetResponse())
            {
                if (httpWebResponse.StatusCode != HttpStatusCode.OK)
                {
                    Debug.LogWarning($"GetHttpWebResponseLength {config.url} error {httpWebResponse.StatusCode}");
                    _status = BackgroundDownloadStatus.Failed;
                    return;
                }
                _totalLength = httpWebResponse.ContentLength;
            }
            
            string filePath = Path.Combine(DataPath, config.filePath);
            FileStream fileStream;
            if (File.Exists(filePath))
            {
                fileStream = File.OpenWrite(filePath);
                _startDownloadPos = fileStream.Length;
                fileStream.Seek(_startDownloadPos, SeekOrigin.Current);//移动文件流中的当前指针
                Debug.Log($"continue download {filePath}");
            }
            else
            {
                fileStream = new FileStream(filePath, FileMode.Create);
                Debug.Log($"start download {filePath}");
            }

            if (_startDownloadPos == _totalLength)
            {
                fileStream.Dispose();
                _status = BackgroundDownloadStatus.Done;
                return;
            }
            else if (_startDownloadPos > _totalLength)
            {
                fileStream.Dispose();
                File.Delete(filePath);
                Debug.LogWarning($"{filePath} cache file error!");
                _status = BackgroundDownloadStatus.Failed;
                return;
            }
            HttpWebRequest webRequest = HttpWebRequest.CreateHttp(config.url);
            webRequest.Method = "GET";
            webRequest.Timeout = 6000;
            if (_startDownloadPos > 0)
            {
                webRequest.AddRange(_startDownloadPos); //设置Range值  
            }
            Stream webStream;
            try
            {
                HttpWebResponse webResponse = (HttpWebResponse)webRequest.GetResponse();
                webStream = webResponse.GetResponseStream();
            }
            catch (Exception e)
            {
                fileStream.Dispose();
                Debug.LogWarning($"{e}");
                _status = BackgroundDownloadStatus.Failed;
                return;
            }
            byte[] by = new byte[4096];
            _childThread = new Thread(() =>
            {
                try
                {
                    while (true)
                    {
                        int readSize = webStream.Read(by, 0, by.Length);
                        if (readSize != 0)
                        {
                            fileStream.Write(by, 0, readSize);
                            _downloadedByteCount += readSize;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning(e.ToString());
                    _status = BackgroundDownloadStatus.Failed;
                }
                finally
                {
                    webStream?.Dispose();
                    webStream = null;
                    fileStream?.Dispose();
                    fileStream = null;
                    _status = BackgroundDownloadStatus.Done;
                }
            });
            _childThread.Start();
        }


        public override bool keepWaiting { get { return _status == BackgroundDownloadStatus.Downloading; } }

        protected override float GetProgress() { return (_startDownloadPos + _downloadedByteCount) / (float)_totalLength; }

        internal static Dictionary<string, BackgroundDownload> LoadDownloads()
        {
            return new Dictionary<string, BackgroundDownload>();
        }

        internal static void SaveDownloads(Dictionary<string, BackgroundDownload> downloads)
        {
        }
    }
}

#endif
