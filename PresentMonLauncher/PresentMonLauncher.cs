﻿//This program was created to be used alongside the PresentMon64.exe application which comes bundled in this installer package.
//I am re-distributing the PresentMon application free of charge with no claim to ownership or copyright
//I'm sure there was a billion and one better ways to write this application, but I'm not a professional programmer, I make videos for a living and am limited in programming ability.
//Please don't sue me if I do something wrong, I'm just an idiot trying to help so please contact inbox@techteamgb.com and I'll happily attempt to fix my mistakes... Thanks!

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Media;

namespace PresentMonLauncher
{
    public partial class PresentMonLauncher : Form
    {
        string textstring = "";
        string workingdir = "";
        bool restoring = false;
        List<GameData> games = new List<GameData>();
        string GameDataFile = Path.Combine(Application.StartupPath, Properties.Settings.Default.GameDataFile);
        string selprof = "";
        GlobalKeyboardHook globalHotKey;
        bool IsRecordingManually = false;
        Process PresentMon64;
        static System.Windows.Forms.Timer aTimer = new System.Windows.Forms.Timer();

        public PresentMonLauncher()
        {
            InitializeComponent();
            LoadGameData();
            GenerateProcessList(process_list);
            loadConfigs();
            CreateGlobalHotkey();
            over_ride_check();
        }


        private void launch_Click(object sender, EventArgs e)
        {
            if (process_list.SelectedIndex == -1)
            {
                MessageBox.Show("Please select a process.");
                return;
            }

            textstring = "-process_name " + Convert.ToString(process_list.SelectedItem) + ".exe";

            if (delay_updown.Value != 0)
                textstring += " -delay " + (int)delay_updown.Value;

            if (time_updown.Value != 0)
                textstring += " -timed " + (int)time_updown.Value;

            if (outputcheck.Checked)
                textstring = textstring + " -output_file " + outputfile.Text;

            if (etlcheck.Checked)
                textstring = textstring + " -etl_file " + etlfile.Text;

            if (!outputcheck.Checked && process_list.CheckedItems.Count > 0)
            {
                textstring +=
                (" -output_file \"" + process_list.SelectedItem.ToString()
                + DateTime.Now.Day.ToString() + '-'
                + DateTime.Now.Hour.ToString() + '-'
                + DateTime.Now.Minute.ToString() + '-'
                + DateTime.Now.Second.ToString() + ".csv\"");
            }

            if (nocsv.Checked)
                textstring = textstring + " -no_csv";

            if (Simple.Checked)
                textstring = textstring + " -simple";

            if (exclude.Checked)
                textstring += " -exclude_dropped ";

            if (process_list.SelectedIndex == -1)
            {
                textstring = textstring + " " + outputfile.Text;
                MessageBox.Show("Please select a process to trace.");
                return; // Added return here to ensure that the program does not continue needlessly.
                        // Props to whomever added this code block.
            }


            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = @"C:\PresentMonLauncher\PresentMon64.exe";
            startInfo.Arguments = textstring;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            // Please re-examine this code block. It is likely that other exceptions can be thrown up other than Win32Exceptions
            try
            {
                if (PresentMon64 == null || PresentMon64.HasExited)
                {
                    PresentMon64 = Process.Start(startInfo);
                    if (time_updown.Value == 0)
                    {
                        IsRecordingManually = true;
                    }
                }
                else
                {
                    PresentMon64.Kill();
                    IsRecordingManually = false;
                    PresentMon64 = Process.Start(startInfo);
                    if (time_updown.Value == 0)
                    {
                        IsRecordingManually = true;
                    }
                }
            }

            catch (Win32Exception ex)
            {
                MessageBox.Show("The program could not find PresentMon64.exe, please place it C:\\PresentMonLauncher");
                MessageBox.Show(ex.ToString());
            }
            // v For catching miscellaneous random errors that may happen.
            catch (Exception ex)
            {
                MessageBox.Show("Source: " + ex.Source + "\nMessage: " + ex.Message);
            }

            // Without this reset the program will not reconfigure correctly.
            textstring = "";

            //Sound stuff
            System.Media.SystemSounds.Beep.Play();
            if (time_updown.Value != 0)
            {
                int secs;
                secs = (Convert.ToInt32(time_updown.Value) + 2) * 1000;
                aTimer.Interval = secs;
                aTimer.Tick += new EventHandler(TimerEvent);
                aTimer.Start();
            }
        }


        private void process_list_SelectedIndexChanged(object sender, EventArgs e)
          => currentflags.Text = Convert.ToString(process_list.SelectedItem) + ".exe";


        private void refresh_Click(object sender, EventArgs e)
        {
            GenerateProcessList(process_list);
        }

        private void LoadGameData()
        {
            if (File.Exists(GameDataFile))
            {
                string jsonData = File.ReadAllText(GameDataFile);
                JArray parsedJson = JArray.Parse(jsonData);

                foreach (var item in parsedJson)
                {
                    GameData thisGame = new GameData();
                    thisGame.Name = item["Name"].ToString();
                    thisGame.Executable = item["Exe"].ToString().Substring(0, item["Exe"].ToString().Length - 4);
                    switch (item["Engine"].ToString())
                    {
                        case "DirectX9":
                            thisGame.Engine = API.DirectX9;
                            break;
                        case "DirectX10":
                            thisGame.Engine = API.DirectX10;
                            break;
                        case "DirectX11":
                            thisGame.Engine = API.DirectX11;
                            break;
                        case "DirectX12":
                            thisGame.Engine = API.DirectX12;
                            break;
                        case "OpenGL":
                            thisGame.Engine = API.OpenGL;
                            break;
                        case "Vulkan":
                            thisGame.Engine = API.Vulkan;
                            break;
                        case "Unknown":
                            thisGame.Engine = API.Unknown;
                            break;
                        default:
                            thisGame.Engine = API.Unknown;
                            break;
                    }
                    games.Add(thisGame);
                }
            }
        }

        private void GenerateProcessList(CheckedListBox target)
        {
            List<string> ProcessNames = new List<string>();
            bool foundRunningApplication = false;

            target.Items.Clear();

            foreach (Process p in Process.GetProcesses())
            {
                ProcessNames.Add(p.ProcessName);
            }

            ProcessNames.Sort();

            if (games.Count > 0)
            {
                foreach (GameData game in games)
                {
                    if (ProcessNames.Contains(game.Executable))
                    {
                        ProcessNames.Remove(game.Executable);
                        ProcessNames.Insert(0, game.Executable);
                        foundRunningApplication = true;
                    }
                }
            }

            foreach (string item in ProcessNames)
            {
                target.Items.Add(item);
            }

            if (foundRunningApplication)
            {
                target.SetItemCheckState(0, CheckState.Checked);
                target.SelectedIndex = 0;

                //auto-loading the approriate config for selected game
                string[] file_list = Directory.GetFiles(Program.default_config_directory, "*.cfg");
                string line;
                string filename;
                if (!ovr_prof_check.Checked)
                {
                    foreach (string item in file_list)
                    {
                        StreamReader read_stream = new StreamReader(File.OpenRead(item));
                        line = read_stream.ReadLine();
                        filename = line.Split(':')[1].Trim();
                        if (filename == target.SelectedItem.ToString())
                        {
                            displayCurrentConfig(item);
                            int fileindex = config_dropdown.Items.IndexOf(Path.GetFileNameWithoutExtension(item));
                            selprof = (Path.GetFileNameWithoutExtension(item));
                            read_stream.Close();
                            return;
                        }
                        read_stream.Close();
                    }
                }
            }
        }

        private void openfolder_Click(object sender, EventArgs e)
        {
            try
            {
                workingdir = Program.default_config_directory;
                Process.Start(Directory.GetCurrentDirectory().ToString());
            }
            catch (Win32Exception ex)
            {
                MessageBox.Show("The program done goofed. Not sure why, try opening the folder manually.");
                MessageBox.Show(ex.ToString());
            }
        }


        private void bencher_Click(object sender, EventArgs e)
        {
            //Process.Start("presentmonbencher.exe");
            BencherWindow bencher = new BencherWindow();
            bencher.Show();
        }


        // The purpose of this one's to check/uncheck appropriately and ensure only
        //   one item is visually checked.
        private void process_list_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            // If there's an item checked, else ignore.
            if (process_list.CheckedItems.Count == 1)
            {
                // Determine if unchecking on the basis of whether or not the item is already checked.
                bool unchecking = (e.CurrentValue == CheckState.Checked);

                // If unchecking,
                //  Ensure next one is unchecked.
                // else
                //  ensure that there's only one checked index.
                if (unchecking)
                    e.NewValue = CheckState.Unchecked;

                else
                {
                    // This involves delegates. If you don't know what it's doing, do not tamper.
                    int item_index = process_list.CheckedIndices[0];
                    process_list.ItemCheck -= process_list_ItemCheck;
                    process_list.SetItemChecked(item_index, false);
                    process_list.ItemCheck += process_list_ItemCheck;
                }
                return;
            }
        }


        private void save_config_button_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfdialog = new SaveFileDialog();
            sfdialog.Filter = "Configuration File|*.cfg";
            sfdialog.Title = "Save Configuration Details";

            if (Directory.Exists(Path.Combine(Application.StartupPath, "config")))
                sfdialog.InitialDirectory = Path.Combine(Application.StartupPath, "config");

            string save_to = "";

            DialogResult save_cancel = sfdialog.ShowDialog();

            if (save_cancel == DialogResult.OK)
                save_to = sfdialog.FileName;
            else
                return;

            if (File.Exists(save_to))
                File.Delete(save_to);

            // Will overwrite.
            using (FileStream fs = File.Open(save_to, FileMode.Create))
            {
                StreamWriter write_stream = new StreamWriter(fs);

                if (process_list.SelectedIndex != -1)
                    write_stream.WriteLine("Process: " + process_list.SelectedItem.ToString());

                if (Simple.Checked)
                    write_stream.WriteLine("Simple: yes");

                if (nocsv.Checked)
                    write_stream.WriteLine("NoCSV: yes");

                if (exclude.Checked)
                    write_stream.WriteLine("Exclude: yes");



                if (time_updown.Value > 0)
                    write_stream.WriteLine("Time: " + (int)time_updown.Value);

                if (delay_updown.Value > 0)
                    write_stream.WriteLine("Delay: " + (int)delay_updown.Value);

                if (!string.IsNullOrEmpty(outputfile.Text))
                    write_stream.WriteLine("Flags: " + outputfile.Text);

                write_stream.Close();
            }

            loadConfigs();
        }


        private void load_config_button_Click(object sender, EventArgs e)
        {
            config_dropdown.SelectedIndex = -1;

            // Create dialog info.
            OpenFileDialog ofdialog = new OpenFileDialog();
            ofdialog.Filter = "Configuration File|*.cfg";
            ofdialog.Title = "Open Configuration File";

            if (Directory.Exists(Path.Combine(Application.StartupPath, "config")))
                ofdialog.InitialDirectory = Path.Combine(Application.StartupPath, "config");


            // Save user input data.
            string open_file = "";

            // Show open file dialog.
            DialogResult save_cancel = ofdialog.ShowDialog();

            // If "OK" then save file name,
            // Else exit function.
            if (save_cancel == DialogResult.OK)
                open_file = ofdialog.FileName;
            else
                return;

            displayCurrentConfig(open_file);
        }


        private void config_dropdown_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!restoring)
                if (config_dropdown.SelectedIndex != -1)
                    displayCurrentConfig(Program.default_config_directory + config_dropdown.SelectedItem.ToString() + ".cfg");
        }


        public void loadConfigs()
        {
            config_dropdown.Items.Clear();

            string[] file_list = Directory.GetFiles(Program.default_config_directory, "*.cfg");

            // This will alphabetize the files (hopefully).
            file_list = file_list.OrderBy(n => n).ToArray();

            foreach (string val in file_list)
                config_dropdown.Items.Add(Path.GetFileNameWithoutExtension(val));

            if (selprof != "")
            {
                config_dropdown.SelectedIndex = config_dropdown.FindStringExact(selprof);
            }
        }


        public void displayCurrentConfig(string filename)
        {
            StreamReader read_stream = new StreamReader(File.OpenRead(filename));
            string line;
            string[] values;

            while (!read_stream.EndOfStream)
            {
                line = read_stream.ReadLine();
                values = line.Split(new char[1] { ' ' }, 2);

                // Skip cases where it's defined but no value.
                if (values.Length < 2)
                    continue;

                if (values[0] == "Process:")
                {
                    if (process_list.Items.Contains(values[1]))
                    {
                        process_list.SetItemChecked(process_list.Items.IndexOf(values[1]), true);
                        currentflags.Text = values[1] + ".exe";
                        process_list.SelectedIndex = process_list.Items.IndexOf(values[1]);
                    }
                    else
                        MessageBox.Show("Process in loaded configuation file not found. Please manually select it.");
                }

                else if (values[0] == "Simple:")
                    Simple.Checked = (values[1] == "yes");

                else if (values[0] == "NoCSV:")
                    nocsv.Checked = (values[1] == "yes");

                else if (values[0] == "Flags:")
                    outputfile.Text = values[1];

                else if (values[0] == "Exclude:")
                    exclude.Checked = (values[1] == "yes");

                else if (values[0] == "Time:")
                    time_updown.Value = Convert.ToDecimal(values[1]);

                else if (values[0] == "Delay:")
                    delay_updown.Value = Convert.ToDecimal(values[1]);


            }

            read_stream.Close();

            restoring = true;

            int temp = config_dropdown.SelectedIndex;
            loadConfigs();
            //config_dropdown.SelectedIndex = (config_dropdown.Items.Count >= temp -1) ? temp : -1;

            restoring = false;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Override the default output path." + Environment.NewLine + Environment.NewLine + "CSV Column Explanations" + Environment.NewLine + "-Dropped: boolean indication. 1 = dropped, 0 = displayed" + Environment.NewLine + "-MSBetweenPresents: time between this Present() API call and the previous one." + Environment.NewLine + "-MsBetweenDisplayChange: time between when this frame was displayed and the previous was displayed." + Environment.NewLine + "-MsInPresentAPI: time spend inside the Present() API call." + Environment.NewLine + "-MsUntilRenderComplete: time between present start and GPU work completion." + Environment.NewLine + "-MsUntilDisplayed: time between present start and frame display.");
        }

        private void button4_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Consume events from an ETL file instead of real-time.");
        }

        private void button1_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfdialog = new SaveFileDialog();
            sfdialog.Filter = "Comma Seperated Values|*.csv";
            sfdialog.Title = "Save Output File";

            if (Directory.Exists(Path.Combine(Application.StartupPath)))
                sfdialog.InitialDirectory = Path.Combine(Application.StartupPath);

            string save_to = "";
            string fileName = string.Empty;
            DialogResult save_cancel = sfdialog.ShowDialog();

            if (save_cancel == DialogResult.OK)
                save_to = sfdialog.FileName;
            else
                return;

            if (File.Exists(save_to))
                File.Delete(save_to);

            // Will overwrite.


            fileName = Path.GetDirectoryName(save_to) + "\\" + Path.GetFileName(save_to);

            outputfile.Text = fileName;


        }

        private void etlbutton_Click(object sender, EventArgs e)
        {
            // Create dialog info.
            OpenFileDialog ofdialog = new OpenFileDialog();
            ofdialog.Filter = "ETL File|*.etl";
            ofdialog.Title = "Open ETL Data";

            // Save user input data.
            string open_file = "";
            string fileName = string.Empty;
            // Show open file dialog.
            DialogResult save_cancel = ofdialog.ShowDialog();

            // If "OK" then save file name,
            // Else exit function.
            if (save_cancel == DialogResult.OK)
                open_file = ofdialog.FileName;
            else
                return;


            fileName = Path.GetDirectoryName(open_file) + "\\" + Path.GetFileName(open_file);

            etlfile.Text = fileName;
        }

        private void submenu_About_Click(object sender, EventArgs e)
        {
            frm_About a = new frm_About();
            a.ShowDialog();
        }

        private void submenu_Exit_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void submenu_Options_Click(object sender, EventArgs e)
        {
            frm_Options o = new frm_Options();
            o.ShowDialog();
            //  This check is probably not necessary, but there might be some weird
            //  condition that causes globalHotKey to end up NULL
            if (globalHotKey != null)
            {
                globalHotKey.unhook();
            }
            CreateGlobalHotkey();
        }

        private void HotkeyEventHandler(object sender, EventArgs e)
        {
            //  This is the method to handle the hotkey
        }

        private void CreateGlobalHotkey()
        {
            globalHotKey = new GlobalKeyboardHook();

            foreach (string token in Properties.Settings.Default.Hotkey.Split('+'))
            {
                switch (token)
                {
                    case "CTRL":
                        globalHotKey.Ctrl = true;
                        break;
                    case "ALT":
                        globalHotKey.Alt = true;
                        break;
                    case "WIN":
                        globalHotKey.Windows = true;
                        break;
                    case "SHIFT":
                        globalHotKey.Shift = true;
                        break;
                    default:
                        globalHotKey.HookedKeys.Add((Keys)Enum.Parse(typeof(Keys), token));
                        break;
                }
            }

            RegisterGlobalHotkey(globalHotKey, HotKeyEvent);
            hotkey_label.Text = Properties.Settings.Default.Hotkey.ToString();
        }

        private void RegisterGlobalHotkey(GlobalKeyboardHook HotKey, KeyEventHandler target)
        {
            HotKey.KeyDown += target;
        }

        private void HotKeyEvent(Object sender, KeyEventArgs e)
        {
            if (IsRecordingManually == true)
            {
                PresentMon64.Kill();
                IsRecordingManually = false;
            }
            else
            {
                GenerateProcessList(process_list);

                launch_Click(this, null);
            }
        }

        private void PresentMonLauncher_FormClosing(object sender, FormClosingEventArgs e)
        {
            foreach (Process p in Process.GetProcessesByName("PresentMon64")) { p.Kill(); }
        }
        private void hotkey_label_MouseClick(object sender, MouseEventArgs e)
        {
            frm_Options o = new frm_Options();
            o.ShowDialog();
            //  This check is probably not necessary, but there might be some weird
            //  condition that causes globalHotKey to end up NULL
            if (globalHotKey != null)
            {
                globalHotKey.unhook();
            }
            CreateGlobalHotkey();

        }

        private void label3_MouseClick(object sender, MouseEventArgs e)
        {
            frm_Options o = new frm_Options();
            o.ShowDialog();
            //  This check is probably not necessary, but there might be some weird
            //  condition that causes globalHotKey to end up NULL
            if (globalHotKey != null)
            {
                globalHotKey.unhook();
            }
            CreateGlobalHotkey();
        }
        private void over_ride_check()
        {
            Size normal = new Size(666, 268);
            Size expanded = new Size(666, 482);
            if (override_check.Checked)
            {
                this.Size = expanded;
                process_list.Enabled = true;
                refresh.Enabled = true;
                stopbutton.Enabled = true;
                launch.Enabled = true;
            }
            else
            {
                this.Size = normal;
                process_list.Enabled = false;
                refresh.Enabled = false;
                stopbutton.Enabled = false;
                launch.Enabled = false;
            }
        }

        private void override_check_CheckedChanged(object sender, EventArgs e)
        {
            over_ride_check();
        }

        private void panel3_MouseClick(object sender, MouseEventArgs e)
        {
            frm_Options o = new frm_Options();
            o.ShowDialog();
            //  This check is probably not necessary, but there might be some weird
            //  condition that causes globalHotKey to end up NULL
            if (globalHotKey != null)
            {
                globalHotKey.unhook();
            }
            CreateGlobalHotkey();
        }

        private void stopbutton_Click(object sender, EventArgs e)
        {
            if (PresentMon64 != null)
            {
                if (IsRecordingManually == true)
                {
                    PresentMon64.Kill();
                    IsRecordingManually = false;
                }
                else
                {
                    PresentMon64.Kill();
                }
            }
            else
            {
                //had to add this to solve the issue of the user pressing stop twice and breaking the program
                return;
            }
            System.Media.SystemSounds.Beep.Play(); //I tried doing this in a function of it's own but it wasn't worth the pain.
        }

        private void hk_button_Click(object sender, EventArgs e)
        {
            frm_Options o = new frm_Options();
            o.ShowDialog();
            if (globalHotKey != null)
            {
                globalHotKey.unhook();
            }
            CreateGlobalHotkey();
        }
        private static void TimerEvent(Object obj, EventArgs e)
        {
            aTimer.Stop();
            System.Media.SystemSounds.Beep.Play();
        }
    }
}
