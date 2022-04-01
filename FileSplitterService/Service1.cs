using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using log4net;


namespace FileSplitterService

{
    //private static readonly ILog _log = LogManager.GetLogger(typeof(FileSplitterService));
        public partial class Service1 : ServiceBase
    {
        private static readonly log4net.ILog _log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        ILog log = log4net.LogManager.GetLogger(typeof(Service1));
        Timer timer = new Timer(); // name space(using System.Timers;)  
        public Service1()
        {
            InitializeComponent();
            log4net.Config.BasicConfigurator.Configure();
        }

        public void OnDebug()
        {
            OnStart(null);
        }

        protected override void OnStart(string[] args)
        {
            _log.Info("Starting the Application");
            timer.Elapsed += new ElapsedEventHandler(OnElapsedTime);
            timer.Interval = Properties.Settings.Default.CycleTimeMinutes*60000; //number in milliseconds  
            timer.Enabled = true;
        }

        protected override void OnStop()
        {
            _log.Info ("Service is stopped");
        }
        private void OnElapsedTime(object source, ElapsedEventArgs e)
        {
            _log.Info ("Service is recall");
            CheckDirectories();
            ProcessFiles();
        }

        public void CheckDirectories()
        {
            _log.Info("Checking if the folders exist");
            if (!Directory.Exists(Properties.Settings.Default.PickUpFolder))
            {
                Directory.CreateDirectory(Properties.Settings.Default.PickUpFolder);
                _log.Info("Created " + Properties.Settings.Default.PickUpFolder + " folder");
            }
            if (!Directory.Exists(Properties.Settings.Default.ArchiveFolder))
            {
                Directory.CreateDirectory(Properties.Settings.Default.ArchiveFolder);
                _log.Info("Created " + Properties.Settings.Default.ArchiveFolder + " folder");
            }
            if (!Directory.Exists(Properties.Settings.Default.OutPutFolder))
            {
                Directory.CreateDirectory(Properties.Settings.Default.OutPutFolder);
                _log.Info("Created " + Properties.Settings.Default.OutPutFolder + " folder");
            }
        }

        public static FileInfo GetNewestFile(DirectoryInfo directory)
        {
            return directory.GetFiles()
                .Union(directory.GetDirectories().Select(d => GetNewestFile(d)))
                .OrderByDescending(f => (f == null ? DateTime.MinValue : f.LastWriteTime))
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns latest writen file from the specified directory.
        /// If the directory does not exist or doesn't contain any file, DateTime.MinValue is returned.
        /// </summary>
        /// <param name="directoryInfo">Path of the directory that needs to be scanned</param>
        /// <returns></returns>
        private static DateTime GetLatestWriteTimeFromFileInDirectory(DirectoryInfo directoryInfo)
        {
            if (directoryInfo == null || !directoryInfo.Exists)
                return DateTime.MinValue;

            FileInfo[] files = directoryInfo.GetFiles();
            DateTime lastWrite = DateTime.MinValue;

            foreach (FileInfo file in files)
            {
                if (file.LastWriteTime > lastWrite)
                {
                    lastWrite = file.LastWriteTime;
                }
            }

            return lastWrite;
        }

        /// <summary>
        /// Returns file's latest writen timestamp from the specified directory.
        /// If the directory does not exist or doesn't contain any file, null is returned.
        /// </summary>
        /// <param name="directoryInfo">Path of the directory that needs to be scanned</param>
        /// <returns></returns>
        private static FileInfo GetLatestWritenFileFileInDirectory(DirectoryInfo directoryInfo)
        {
            if (directoryInfo == null || !directoryInfo.Exists)
                return null;

            FileInfo[] files = directoryInfo.GetFiles();
            DateTime lastWrite = DateTime.MinValue;
            FileInfo lastWritenFile = null;

            foreach (FileInfo file in files)
            {
                if (file.LastWriteTime > lastWrite)
                {
                    lastWrite = file.LastWriteTime;
                    lastWritenFile = file;
                }
            }
            return lastWritenFile;
        }

        public void ProcessFiles()
        {
            //https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/linq/how-to-split-a-file-into-many-files-by-using-groups-linq
            //
            //Looking for files in the pickup directory
            _log.Info("Starting to look for files");
            if (System.IO.Directory.Exists(Properties.Settings.Default.PickUpFolder))
            {
                var PickUpDirPath = Properties.Settings.Default.PickUpFolder;
                _log.Info("Looking in " + PickUpDirPath + " pickup folder");

                var PickUp_Dir = new System.IO.DirectoryInfo(Properties.Settings.Default.PickUpFolder);
                _log.Info("Getting directory infomation from: " + PickUp_Dir);

                var ArchiveDirPath = Properties.Settings.Default.ArchiveFolder;
                _log.Info("Setting the " + ArchiveDirPath + " archive folder");

                var Archive_Dir = new System.IO.DirectoryInfo(Properties.Settings.Default.ArchiveFolder);
                _log.Info("Getting directory infomation from: " + Archive_Dir);

                var OutDirPath = Properties.Settings.Default.OutPutFolder;
                _log.Info("Setting the " + OutDirPath + " out bound folder");

                var Out_Dir = new System.IO.DirectoryInfo(Properties.Settings.Default.OutPutFolder);
                _log.Info("Getting directory infomation from: " + Out_Dir);

                //var PickUpFiles = from file in PickUp_Dir.GetFileSystemInfos() select file.Name; //and file.LastWriteTime > DateAdd(DateInterval.Hour, -12, OrderEntryDate) And file.LastWriteTime < DateAdd(DateInterval.Hour, 12, OrderEntryDate) Order By file.LastWriteTime Descending Select file

                FileInfo[] PickUpFiles = PickUp_Dir.GetFiles().OrderBy(p => p.CreationTime).ToArray();

                if (!(PickUpFiles == null))
                {
                    foreach (FileInfo filename in PickUpFiles)
                    {
                        _log.Info("Processing file " + filename);
                        int cntFile = 1;
                        const int MAX_LINES = 1;
                        try
                        {
                            string fullPath = PickUpDirPath + "\\" + filename;
                            //_log.Info("PickUp filename: " + fullPath);

                            string fullArchivePath = ArchiveDirPath + "\\" + filename + "_" + DateTime.Now.ToString("yyyy_MM_dd_HHmmssfff") + ".csv";
                            //_log.Info("Archive filename: " + fullArchivePath);

                            string fullOutput = OutDirPath + "\\" + filename + "_{0}.csv";
                            //_log.Info("Out filename:" + fullOutput);

                            _log.Info("Backing up the file in the archive directory");
                            File.Copy(fullPath, fullArchivePath);

                            _log.Info("Calling the file split function");
                            var reader = File.OpenText(fullPath);
                            while (!reader.EndOfStream)
                            {
                                var writer = File.CreateText(String.Format(fullOutput, cntFile));
                                _log.Info("Creating file: " + string.Format(fullOutput, cntFile));
                                for (int idx = 0; idx < MAX_LINES; idx++)
                                {
                                    writer.Write(reader.ReadLine());
                                    _log.Info("Wrote to file: " + reader.ReadLine());
                                    if (reader.EndOfStream) break;
                                }
                                writer.Close();
                                cntFile++;
                            }
                            reader.Close();

                            _log.Info("Deleting original file");
                            File.Delete(fullPath);

                        }
                        catch (Exception ex)
                        {
                            _log.Info("File " + filename + " has an issue");
                        }
                        finally
                        {
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                        }
                    }
                    _log.Info("Completed");
                }
                else
                {
                    _log.Info("No files to process");
                    return;
                }
            }
            else
            {
                _log.Info("PickUp directory does not exist");
            }
        }
        
    }
}
