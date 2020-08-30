using Microsoft.WindowsAPICodePack.Dialogs;
using PortableDevices;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PhotoOrganizer
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MahApps.Metro.Controls.MetroWindow
    {
        PortableDeviceCollection collection;
        PortableDeviceFolder folder;
        List<PortableDeviceFolder> foundFolders = new List<PortableDeviceFolder>();
        CommonOpenFileDialog FolderBrowser = new CommonOpenFileDialog();
        int existsCounter = 0;
        Dictionary<string, string> DestPathes = new Dictionary<string, string>();

        string[] monthNames = { "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند" };

        enum DateMethod
        {
            DateModified,
            JpegData,
            FileName
        }

        public MainWindow()
        {
            InitializeComponent();


            FolderBrowser.Title = "Select Folder";
            FolderBrowser.IsFolderPicker = true;
            FolderBrowser.AddToMostRecentlyUsedList = false;
            FolderBrowser.AllowNonFileSystemItems = false;
            FolderBrowser.EnsureFileExists = true;
            FolderBrowser.EnsurePathExists = true;
            FolderBrowser.EnsureReadOnly = false;
            FolderBrowser.EnsureValidNames = true;
            FolderBrowser.Multiselect = false;
            FolderBrowser.ShowPlacesList = true;


        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            RefreshDevicesList();
        }

        private void RefreshDevicesList()
        {
            DevicesListBox.Items.Clear();
            collection = new PortableDeviceCollection();
            collection.Refresh();
            foreach (var device in collection)
            {
                device.Connect();
                DevicesListBox.Items.Add(device.FriendlyName);
                device.Disconnect();
            }
            DevicesListBox.Items.Add("Load from disk");
        }

        private void MetroWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshDevicesList();
            Progress.Visibility = Visibility.Collapsed;

            Step1NextButtonEnabled();
        }

        private async void Step3NextButton_Click(object sender, RoutedEventArgs e)
        {
            ProgressLabel.Content = "Looking for files...";
            Progress.Visibility = Visibility.Visible;
            Step3.Visibility = Visibility.Collapsed;

            if (DevicesListBox.SelectedIndex > (collection.Count - 1))
            {
                //Disk
                var dialog = new CommonOpenFileDialog();
                dialog.IsFolderPicker = true;
                CommonFileDialogResult result = dialog.ShowDialog();

                if (result == CommonFileDialogResult.Ok)
                {
                    string path = dialog.FileName;

                    string[] files = System.IO.Directory.GetFiles(path, "*", SearchOption.AllDirectories);

                    ProgressR.Visibility = Visibility.Collapsed;
                    ProgressB.Visibility = Visibility.Visible;

                    ProgressLabel.Content = "Copying photos...";
                    var photos = from b in files
                                 let fn = System.IO.Path.GetFileName(b)
                                 where (((System.IO.Path.GetExtension(fn).ToLower().Contains("jpg")) && (!fn.ToLower().Contains("highres"))) || (System.IO.Path.GetExtension(fn).ToLower().Contains("nar")) || (System.IO.Path.GetExtension(fn).ToLower().Contains("thm")) || (System.IO.Path.GetExtension(fn).ToLower().Contains("tnl")))
                                 select b;

                    await CopyFiles(photos, Properties.Settings.Default.Path1, Properties.Settings.Default.DeletePhotos, "", DateMethod.JpegData, "");

                    ProgressLabel.Content = "Copying high resolution photos...";
                    var hrphotos = from b in files
                                   let fn = System.IO.Path.GetFileName(b)
                                   where ((System.IO.Path.GetExtension(fn).ToLower().Contains("jpg")) && (fn.ToLower().Contains("highres")))
                                   select b;

                    await CopyFiles(hrphotos, Properties.Settings.Default.Path1, Properties.Settings.Default.DeleteHighResPhotos, "highres", DateMethod.JpegData, "");

                    DestPathes.Clear(); //Avoid copying videos to photos folder.
                    ProgressLabel.Content = "Copying videos...";
                    // For some odd reason, videos doesn't have extension!!
                    var videos = from b in files
                                 let fn = System.IO.Path.GetFileName(b)
                                 where !((System.IO.Path.GetExtension(fn).ToLower().Contains("jpg")) || (System.IO.Path.GetExtension(fn).ToLower().Contains("nar")) || (System.IO.Path.GetExtension(fn).ToLower().Contains("thm")) || (System.IO.Path.GetExtension(fn).ToLower().Contains("tnl")))
                                 select b;


                    await CopyFiles(videos, Properties.Settings.Default.Path2, Properties.Settings.Default.DeleteVideos, "", DateMethod.FileName, "");

                    Progress.Visibility = Visibility.Collapsed;
                    Step5.Visibility = Visibility.Visible;
                }
                else
                {
                    Progress.Visibility = Visibility.Collapsed;
                    Step3.Visibility = Visibility.Visible;
                }
            }
            else
            {
                var device = collection[DevicesListBox.SelectedIndex];
                device.Connect();
                await Task.Run(() =>
                {
                    folder = device.GetContents();
                });

                ProgressLabel.Content = "Finding camera folder...";

                foundFolders.Clear();
                FindFolders(folder, DevicesListBox.SelectedItem.ToString());

                device.Disconnect();

                Progress.Visibility = Visibility.Collapsed;
                Step4.Visibility = Visibility.Visible;
            }
        }

        private void FindFolders(PortableDeviceFolder folder, string currentPath)
        {
            if (folder.Name.Contains("Camera Roll"))
            {
                foundFolders.Add(folder);
                FoldersListBox.Items.Add(currentPath + "\\" + folder.Name);
            }
            foreach (var item in folder.Files)
            {
                if (item is PortableDeviceFolder)
                {
                    FindFolders((PortableDeviceFolder)item, currentPath + "\\" + folder.Name);
                }
            }
        }

        private void DevicesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Step3NextButton.IsEnabled = (DevicesListBox.SelectedIndex >= 0);
        }

        private void browsebutton1_Click(object sender, RoutedEventArgs e)
        {
            if (FolderBrowser.ShowDialog() == CommonFileDialogResult.Ok)
            {
                Properties.Settings.Default["Path1"] = FolderBrowser.FileName;
            }
            Step1NextButtonEnabled();
        }
        private void browsebutton2_Click(object sender, RoutedEventArgs e)
        {
            if (FolderBrowser.ShowDialog() == CommonFileDialogResult.Ok)
            {
                Properties.Settings.Default["Path2"] = FolderBrowser.FileName;
            }
            Step1NextButtonEnabled();
        }

        private void Step1NextButtonEnabled()
        {
            Step1NextButton.IsEnabled = ((Properties.Settings.Default.Path1.Length > 0) && (Properties.Settings.Default.Path2.Length > 0));
        }

        private void Step1NextButton_Click(object sender, RoutedEventArgs e)
        {
            Step2.Visibility = Visibility.Visible;
            Step1.Visibility = Visibility.Collapsed;
            Properties.Settings.Default.Save();
        }

        private void Step2NextButton_Click(object sender, RoutedEventArgs e)
        {
            Step3.Visibility = Visibility.Visible;
            Step2.Visibility = Visibility.Collapsed;
            Properties.Settings.Default.Save();
        }

        private void FoldersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Step4NextButton.IsEnabled = (FoldersListBox.SelectedIndex >= 0);
        }

        private async void Step4NextButton_Click(object sender, RoutedEventArgs e)
        {
            ProgressLabel.Content = "Copying...";
            Progress.Visibility = Visibility.Visible;
            ProgressR.Visibility = Visibility.Collapsed;
            ProgressB.Visibility = Visibility.Visible;
            Step4.Visibility = Visibility.Collapsed;


            var device = collection[DevicesListBox.SelectedIndex];


            PortableDeviceFolder f = foundFolders[FoldersListBox.SelectedIndex];


            ProgressLabel.Content = "Copying photos...";
            var photos = from b in f.Files
                         where (((System.IO.Path.GetExtension(b.Name).ToLower().Contains("jpg")) && (!b.Name.ToLower().Contains("highres"))) || (System.IO.Path.GetExtension(b.Name).ToLower().Contains("nar")) || (System.IO.Path.GetExtension(b.Name).ToLower().Contains("thm")) || (System.IO.Path.GetExtension(b.Name).ToLower().Contains("tnl")))
                         select b;

            await CopyFiles(device, photos, Properties.Settings.Default.Path1, Properties.Settings.Default.DeletePhotos, "", DateMethod.JpegData, "");

            ProgressLabel.Content = "Copying high resolution photos...";
            var hrphotos = from b in f.Files
                           where ((System.IO.Path.GetExtension(b.Name).ToLower().Contains("jpg")) && (b.Name.ToLower().Contains("highres")))
                           select b;

            await CopyFiles(device, hrphotos, Properties.Settings.Default.Path1, Properties.Settings.Default.DeleteHighResPhotos, "highres", DateMethod.JpegData, "");

            DestPathes.Clear(); //Avoid copying videos to photos folder.
            ProgressLabel.Content = "Copying videos...";
            // For some odd reason, videos doesn't have extension!!
            var videos = from b in f.Files
                         where !((System.IO.Path.GetExtension(b.Name).ToLower().Contains("jpg")) || (System.IO.Path.GetExtension(b.Name).ToLower().Contains("nar")) || (System.IO.Path.GetExtension(b.Name).ToLower().Contains("thm")) || (System.IO.Path.GetExtension(b.Name).ToLower().Contains("tnl")))
                         select b;


            await CopyFiles(device, videos, Properties.Settings.Default.Path2, Properties.Settings.Default.DeleteVideos, "", DateMethod.FileName, ".mp4");

            Progress.Visibility = Visibility.Collapsed;
            Step5.Visibility = Visibility.Visible;
        }

        string prevDestinationPath = "";
        int firstF = 0;

        private async Task CopyFiles(IEnumerable<string> files, string path, bool delete, string additionalPath, DateMethod dateMethod, string additionalExtension)
        {
            if (!System.IO.Directory.Exists(path))
                System.IO.Directory.CreateDirectory(path);

            int counter = 0;
            ProgressB.Value = 0;
            ProgressB.Maximum = files.Count();

            DirectoryInfo di = new DirectoryInfo(path);
            firstF = di.GetDirectories("*.*", System.IO.SearchOption.TopDirectoryOnly).Count();
            prevDestinationPath = "";

            string[] existingFiles = (from FileInfo f in di.GetFiles("*.*", SearchOption.AllDirectories)
                                      select f.Name).ToArray();

            foreach (var item in files)
            {
                try
                {
                    if (ignoreCheckBox.IsChecked == true)
                    {
                        if (existingFiles.Contains(item + additionalExtension))
                        {
                            existsCounter++;
                            ProgressMessageLabel.Content = existsCounter.ToString() + " files were already existed. I ignored them.";


                            counter++;
                            ProgressB.Value = counter;
                            continue;
                        }
                    }

                    System.IO.File.Copy(item, System.IO.Path.Combine(path, System.IO.Path.GetFileName(item)));
                    
                    await CopyFile(path, System.IO.Path.GetFileName(item), dateMethod, additionalExtension, additionalPath);


                    counter++;
                    ProgressB.Value = counter;

                    if (delete)
                    {
                        System.IO.File.Delete(item);   
                    }
                }
                catch (Exception ex)
                {
                    //MessageBox.Show("Error while copying " + file.Name + "\n\n" + ex.Message);
                }
            }
        }


        private async Task CopyFiles(PortableDevice device, IEnumerable<PortableDeviceObject> files, string path, bool delete, string additionalPath, DateMethod dateMethod, string additionalExtension)
        {
            if (!System.IO.Directory.Exists(path))
                System.IO.Directory.CreateDirectory(path);

            int counter = 0;
            ProgressB.Value = 0;
            ProgressB.Maximum = files.Count();

            DirectoryInfo di = new DirectoryInfo(path);
            firstF = di.GetDirectories("*.*", System.IO.SearchOption.TopDirectoryOnly).Count();

            string[] existingFiles = (from FileInfo f in di.GetFiles("*.*", SearchOption.AllDirectories)
                                      select f.Name).ToArray();

            PortableDevice pd = collection[DevicesListBox.SelectedIndex];

            foreach (var item in files)
            {
                if (item is PortableDeviceFile)
                {
                    PortableDeviceFile file = (PortableDeviceFile)item;
                    try
                    {
                        if (ignoreCheckBox.IsChecked == true)
                        {
                            if (existingFiles.Contains(file.Name + additionalExtension))
                            {
                                existsCounter++;
                                ProgressMessageLabel.Content = existsCounter.ToString() + " files were already existed. I ignored them.";


                                counter++;
                                ProgressB.Value = counter;
                                continue;
                            }
                        }

                        device.Connect();

                        Exception taskEx = null;
                        await Task.Factory.StartNew(() =>
                        {
                            try
                            {
                                pd.DownloadFile(file, path);
                            }
                            catch (Exception ex)
                            {
                                taskEx = ex;
                            }
                        });
                        if (taskEx != null)
                            throw taskEx;

                        device.Disconnect();

                        await CopyFile(path, file.Name, dateMethod, additionalExtension, additionalPath);


                        counter++;
                        ProgressB.Value = counter;

                        if (delete)
                        {
                            device.Connect();
                            await Task.Factory.StartNew(() =>
                            {
                                pd.DeleteFile(file);
                            });
                            device.Disconnect();
                        }
                    }
                    catch (Exception ex)
                    {
                        //MessageBox.Show("Error while copying " + file.Name + "\n\n" + ex.Message);
                    }
                }
            }
        }

        private async Task CopyFile(string path, string fileName, DateMethod dateMethod, string additionalExtension, string additionalPath)
        {

            string copiedFile = System.IO.Path.Combine(path, fileName /*+ additionalExtension*/);
            System.IO.FileInfo fi = new System.IO.FileInfo(copiedFile);
            DateTime d;
            try
            {
                if (dateMethod == DateMethod.DateModified)
                    d = fi.LastWriteTime;
                else if (dateMethod == DateMethod.JpegData)
                    d = GetDateTakenFromImage(copiedFile);
                else /*if (dateMethod == DateMethod.FileName)*/
                {
                    CultureInfo persianCulture = new CultureInfo("fa-IR");

                    string strDate = fileName.Split(new char[] { '_' })[1];

                    bool persianDate = false;
                    if (int.Parse(strDate.Substring(0, 4)) < 1900)
                        persianDate = true;

                    d = DateTime.ParseExact(fileName.Split(new char[] { '_' })[1],
                              "yyyyMMdd",
                               persianDate ? persianCulture : CultureInfo.InvariantCulture);
                }
            }
            catch
            {
                if (additionalExtension.Length > 0)
                {
                    d = new DateTime(1900, 1, 1);
                }
                else if (dateMethod == DateMethod.FileName) //Use DateModified
                {
                    d = fi.LastWriteTime;
                }
                else //Try using FileName, but if it's not possible, say UNKNOWN.
                {
                    //d = fi.LastWriteTime;

                    List<string> s = fileName.Split(new char[] { '_' }).ToList();
                    if (s.Count > 1)
                    {
                        DateTime.TryParseExact(s[1],
                                          "yyyyMMdd",
                                           CultureInfo.InvariantCulture, DateTimeStyles.None, out d);

                        if (d.Year < 1000)
                        {
                            d = new DateTime(1900, 1, 1);
                        }
                    }
                    else
                    {
                        d = new DateTime(1900, 1, 1);
                    }
                }
            }

            string destinationPath;
            if (d.Year == 1900)
                destinationPath = "Unknown";
            else
                destinationPath = GetDestinationPath(d);

            string completeDestPath;
            if (DestPathes.ContainsKey(destinationPath))
            {
                if (additionalPath.Length > 0)
                    completeDestPath = System.IO.Path.Combine(DestPathes[destinationPath], additionalPath);
                else
                    completeDestPath = DestPathes[destinationPath];
            }
            else
            {
                completeDestPath = GetExistingFolderPath(path, destinationPath);

                if (completeDestPath.Length == 0)
                {
                    if (d.Year == 1900)
                        completeDestPath = System.IO.Path.Combine(path, destinationPath);
                    else
                    {
                        if (prevDestinationPath != destinationPath)
                            firstF += 1;

                        completeDestPath = System.IO.Path.Combine(path, firstF.ToString() + " - " + destinationPath);
                    }
                }
                DestPathes.Add(destinationPath, completeDestPath);

                prevDestinationPath = destinationPath;
            }

            if (!System.IO.Directory.Exists(completeDestPath))
                System.IO.Directory.CreateDirectory(completeDestPath);

            /* We can't determine Creation Date of file before copying it. So, we'll have to copy it anyway. */

            int newFileNum = 1;
            string newFile = System.IO.Path.Combine(completeDestPath, fileName + additionalExtension);

            while (System.IO.File.Exists(newFile))
            {
                //Assuming that ignoreCheckBox.IsChecked == false , because if it was checked, the code won't reach here.
                newFileNum++;
                newFile = System.IO.Path.Combine(completeDestPath, System.IO.Path.GetFileNameWithoutExtension(fileName + additionalExtension) + " (" + newFileNum.ToString() + ")" + System.IO.Path.GetExtension(fileName + additionalExtension));
            }

            await Task.Run(() =>
            {
                System.IO.File.Move(copiedFile, newFile);
            });

        }

        private string GetExistingFolderPath(string path, string destinationPath)
        {
            System.IO.DirectoryInfo di = new System.IO.DirectoryInfo(path);
            foreach (DirectoryInfo item in di.GetDirectories("*.*", System.IO.SearchOption.TopDirectoryOnly))
            {
                if (item.Name.Contains(destinationPath))
                {
                    return item.FullName;
                }
            }
            return "";
        }

        //we init this once so that if the function is repeatedly called
        //it isn't stressing the garbage man
        private static Regex r = new Regex(":");

        //retrieves the datetime WITHOUT loading the whole image
        public static DateTime GetDateTakenFromImage(string path)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (System.Drawing.Image myImage = System.Drawing.Image.FromStream(fs, false, false))
            {
                System.Drawing.Imaging.PropertyItem propItem = myImage.GetPropertyItem(36867);
                string dateTaken = r.Replace(Encoding.UTF8.GetString(propItem.Value), "-", 2);
                return DateTime.Parse(dateTaken);
            }
        }

        private string GetDestinationPath(DateTime d)
        {
            string output;
            PersianCalendar pcalendar = new PersianCalendar();
            int month = pcalendar.GetMonth(d);
            output = (pcalendar.GetYear(d) % 100).ToString("00") + ".";
            output += (month.ToString()) + " " + monthNames[month - 1];
            return output;
        }

        private void Step5NextButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}
