using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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

namespace xmaencode_gui
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Get a temp directory for storing the WAV files.
        public static string TemporaryDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());

        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Browse for songs to add.
        /// </summary>
        private void Button_Songs_Browse(object sender, RoutedEventArgs e)
        {
            VistaOpenFileDialog OpenFileDialog = new()
            {
                Title = "Select Songs",
                Multiselect = true,
                Filter = "All Types|*.*"
            };

            // If the selections are valid, add them to the list of text in the songs textbox.
            if (OpenFileDialog.ShowDialog() == true)
            {
                // Don't erase the box, just add a seperator.
                if (TextBox_Songs.Text.Length != 0)
                    TextBox_Songs.Text += "|";

                // Add selected files to the text box.
                for (int i = 0; i < OpenFileDialog.FileNames.Length; i++)
                    TextBox_Songs.Text += $"{OpenFileDialog.FileNames[i]}|";

                // Remove the extra seperator added at the end.
                TextBox_Songs.Text = TextBox_Songs.Text.Remove(TextBox_Songs.Text.LastIndexOf('|'));
            }
        }
        
        /// <summary>
        /// Browses for the folder to use as the Output Directory.
        /// </summary>
        private void Button_OutputDir_Browse(object sender, RoutedEventArgs e)
        {
            VistaFolderBrowserDialog FolderBrowser = new()
            {
                Description = "Select Output Directory",
                UseDescriptionForTitle = true
            };

            // If the selection is valid then set the Output Directory textbox to the folder we just chose.
            if (FolderBrowser.ShowDialog() == true)
                TextBox_OutputDir.Text = FolderBrowser.SelectedPath;
        }

        /// <summary>
        /// Actually convert the songs to XMA via vgmstream and xmaencode.
        /// </summary>
        private void ConvertSongs(object sender, RoutedEventArgs e)
        {
            // Abort if settings aren't right.
            if(TextBox_Songs.Text == "" || !Directory.Exists(TextBox_OutputDir.Text))
            {
                MessageBox.Show("Either no songs have been specified or the Output Directory does not exist. Please check your settings",
                                "xmaencode-gui",
                                MessageBoxButton.OK,
                                MessageBoxImage.Exclamation);
                return;
            }

            // Get list of song files.
            string[] songsToConvert = TextBox_Songs.Text.Split('|');

            // Create Temporary Directory for the WAV files.
            Directory.CreateDirectory($@"{TemporaryDirectory}\tempWavs");

            // Loop through the list of songs.
            for (int i = 0; i < songsToConvert.Length; i++)
            {
                // Get the file name with an XMA extension for use in writing. As well as set up dummy loop points to fill in later.
                string origName = $"{System.IO.Path.GetFileNameWithoutExtension(songsToConvert[i])}.xma";
                int startLoop = 0;
                int endLoop = 0;

                // If this file isn't a WAV, try convert it using vgmstream.
                if (System.IO.Path.GetExtension(songsToConvert[i]) != ".wav")
                {
                    Process process = new();
                    process.StartInfo.FileName = $"\"{Environment.CurrentDirectory}\\ExternalResources\\vgmstream\\vgmstream-cli.exe\"";
                    process.StartInfo.Arguments = $"-i -o \"{TemporaryDirectory}\\tempWavs\\custom{i}.wav\" \"{songsToConvert[i]}\"";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.Start();

                    StreamReader sr = process.StandardOutput;
                    string[] output = sr.ReadToEnd().Split("\r\n");
                    process.WaitForExit();

                    // If vgmstream has reported any loop points, then store them.
                    foreach (string value in output)
                    {
                        if (value.StartsWith("loop start"))
                        {
                            string[] split = value.Split(' ');
                            startLoop = int.Parse(split[2]);
                        }
                        if (value.StartsWith("loop end"))
                        {
                            string[] split = value.Split(' ');
                            endLoop = int.Parse(split[2]);
                        }
                    }

                    // Set the path to the song to our WAV for the rest of the function.
                    songsToConvert[i] = $@"{TemporaryDirectory}\tempWavs\custom{i}.wav";
                }


                // Convert WAV file to XMA.
                using (Process process = new())
                {
                    process.StartInfo.FileName = $"\"{Environment.CurrentDirectory}\\ExternalResources\\xmaencode.exe\"";
                    process.StartInfo.Arguments = $"\"{songsToConvert[i]}\" /b 64 /t \"{TextBox_OutputDir.Text}\\{origName}\"";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.CreateNoWindow = true;

                    process.Start();
                    process.BeginOutputReadLine();
                    process.WaitForExit();
                }

                // If the user has loop eneabled, then patch the XMA to actually loop from start to end or use the loop points if they exist.
                if (Checkbox_Loop.IsChecked == true)
                {
                    byte[] xma = File.ReadAllBytes($@"{TextBox_OutputDir.Text}\{origName}");
                    for (int x = 0; x < xma.Length; x += 4)
                    {
                        // Find the XMA2 Chunk Header
                        if (xma[x] == 0x58 && xma[x + 1] == 0x4D && xma[x + 2] == 0x41 && xma[x + 3] == 0x32)
                        {
                            // If we haven't fetched any loop points, then just add a start to end loop.
                            if (startLoop == 0 && endLoop == 0)
                            {
                                // Set the part of the file that controls the end loop (0x10 ahead of the XMA2 Chunk Header) to the sample count (0x20 ahead of the XMA2 Chunk Header).
                                xma[x + 0x10] = xma[x + 0x20];
                                xma[(x + 1) + 0x10] = xma[(x + 1) + 0x20];
                                xma[(x + 2) + 0x10] = xma[(x + 2) + 0x20];
                                xma[(x + 3) + 0x10] = xma[(x + 3) + 0x20];
                            }

                            // If we DO have loop points, then add them.
                            else
                            {
                                // Make a byte array out of the values.
                                byte[] startBytes = BitConverter.GetBytes(startLoop);
                                Array.Reverse(startBytes);
                                byte[] endBytes = BitConverter.GetBytes(endLoop);
                                Array.Reverse(endBytes);

                                // Start Loop Bytes
                                xma[x + 0xC] = startBytes[0];
                                xma[(x + 1) + 0xC] = startBytes[1];
                                xma[(x + 2) + 0xC] = startBytes[2];
                                xma[(x + 3) + 0xC] = startBytes[3];

                                // End Loop Bytes
                                xma[x + 0x10] = endBytes[0];
                                xma[(x + 1) + 0x10] = endBytes[1];
                                xma[(x + 2) + 0x10] = endBytes[2];
                                xma[(x + 3) + 0x10] = endBytes[3];

                            }
                        }
                    }
                    // Save the updated XMA.
                    File.WriteAllBytes($@"{TextBox_OutputDir.Text}\{origName}", xma);
                }
            }
        
            // Delete the Temporary Directory.
            Directory.Delete($@"{TemporaryDirectory}\tempWavs", true);
            MessageBox.Show("Done!",
                            "xmaencode-gui",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
        }
    }
}
