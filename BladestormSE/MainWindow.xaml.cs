﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using BladestormSE.Resources;
using Isolib.IOPackage;
using Isolib.STFSPackage;
using Microsoft.Win32;

namespace BladestormSE
{
    public partial class MainWindow
    {
        #region Variables

        private readonly List<Slot> Slot = new List<Slot>();
        private string _filepath;
        private long _offset;
        private Stfs _stfs;
        private byte[] buffer;

        #endregion Variables

        public MainWindow()
        {
            InitializeComponent();
        }

        #region Input

        private void OpenFile()
        {
            try
            {
                var open = new OpenFileDialog
                           {
                               Title = "Open Xbox360 Bladestorm Save",
                               Filter = "BLADESTORM_*|*BLADESTORM_EU;*BLADESTORM_NA",
                               CheckFileExists = true,
                               Multiselect = false
                           };
                SetStatus("Opening Save");
                //If user cancels or otherwise does not select a save, return.
                if (open.ShowDialog() ?? true)
                {
//abort load
                    SetStatus("Idle");
                    return;
                }
                _filepath = open.FileName;
                SetStatus(("Reading " + open.SafeFileName));

                _stfs = new Stfs(_filepath);
                buffer = _stfs.Extract(0);
                ReadID();
                ReadFile();
                SetStatus("Loaded!");
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            }
        }

        private void ReadFile()
        {
            int[] offsets =
            {
                0xE35800,
                0xE35830,
                0xE35850
            };
            var usedslots = new bool[30];
            using (var reader = new Reader(buffer, true))
            {
                var constructor = new StringBuilder();

                //First We determine used slots.
                for (int i = 0; i < 30; i++)
                {
                    SetStatus("Scanning Slot#" + (i + 1).ToString(CultureInfo.InvariantCulture));
                    //Position = Slot#1 offset + (slot length * slot number)
                    reader.Position = offsets[0] + (128*i);
                    //Get Locale/Used Slot
                    string storage = (reader.ReadString(StringType.Ascii, 16));
                    storage = storage.Trim(Convert.ToChar("\0"));
                    //NODATA = Empty Slot
                    if (storage == "NODATA")
                    {
                        //Flag slot as unused
                        usedslots[i] = false;
                        //add it to the list.
                        constructor.Append(storage);
                        SaveSlot.Dispatcher.Invoke(new Action(() => SaveSlot.Items.Add(constructor.ToString())));
                        Slot.Add(new Slot
                                 {
                                     SlotString = constructor.ToString(),
                                     StartingOffset = 0x44 + (0x79400*(i + 1))
                                 });
                        constructor.Clear();
                        //Jump to i + 1 loop
                        continue;
                    }
                    usedslots[i] = true;
                    constructor.Append(storage);
                    //Get Time & Name
                    //2 loops, code is repetitive otherwise
                    for (int o = 1; o < 3; o++)
                    {
                        constructor.Append(", ");
                        //offsets[1] = Time, [2] = Name, hence why we started on 1.
                        //Position = Slot#1 Time/Name offset + (slot length * slot#)
                        reader.Position = offsets[o] + (128*i);
                        storage = (reader.ReadString(StringType.Ascii, 16));
                        storage = storage.Trim(Convert.ToChar("\0"));
                        constructor.Append(storage);
                    }

                    //Build List
                    SaveSlot.Items.Add(constructor.ToString());
                    Slot.Add(new Slot {SlotString = constructor.ToString(), StartingOffset = 0x44 + (0x79400*(i + 1))});
                    constructor.Clear();
                }
                //TODO Add flag for failed load, reset editor state.
                if (Slot.TrueForAll(slot => slot.Used == false))
                {
                    //If no slot is tagged as used, editing is pointless since there's nothing to work with.
                    MessageBox.Show(
                        "This save appears to be empty! /n If this save does contain a used slot, an error has occurred. /n Either way, aborting load operation.",
                        "Error!", MessageBoxButton.OK,
                        MessageBoxImage.Exclamation);
                    return;
                }
                //End Read Slots

                //Begin load first used slot.
                //TODO Clean up the saveslot list to only list used saves.
                //SaveSlot.Items.Clear();
                SaveSlot.SelectedIndex = 0;

                SetStatus("Reading Slot# " + SaveSlot.SelectedIndex + 1);

                _offset = (0x44 + (0x79400*SaveSlot.SelectedIndex));
                try
                {
                    //Name
                    SetStatus("Reading Name");
                    reader.Position = _offset + 4;
                    Slot[SaveSlot.SelectedIndex].Name =
                        reader.ReadString(StringType.Ascii, 16).Trim(Convert.ToChar("\0"));
                    // Slot[SaveSlot.SelectedIndex].Name = CharName.Text = reader.ReadString(StringType.Ascii, 16).Trim(Convert.ToChar("\0"));

                    //Money
                    SetStatus("Reading Money");
                    reader.Position = _offset + 64;
                    Slot[SaveSlot.SelectedIndex].Money = (int) (MoneyBox.Value = reader.ReadInt32());


                    //Knives
                    SetStatus("Reading Knives Level");
                    reader.Position = _offset + 842;
                    Slot[SaveSlot.SelectedIndex].Knivelv = reader.ReadUInt16();
                    SetStatus("Reading Knives Points");
                    reader.Position = _offset + 848;
                    Slot[SaveSlot.SelectedIndex].Knivepoint = reader.ReadInt32();

                    //Rapier
                    SetStatus("Reading Rapier Level");
                    reader.Position = _offset + 926;
                    Slot[SaveSlot.SelectedIndex].Rapierlv = reader.ReadUInt16();
                    SetStatus("Reading Rapier Points");
                    reader.Position = _offset + 932;
                    Slot[SaveSlot.SelectedIndex].Rapierpoint = reader.ReadUInt32();

                    //Sword
                    SetStatus("Reading Sword Level");
                    reader.Position = _offset + 1010;
                    Slot[SaveSlot.SelectedIndex].Swordlv = reader.ReadUInt16();
                    SetStatus("Reading Sword Points");
                    reader.Position = _offset + 1016;
                    Slot[SaveSlot.SelectedIndex].Swordpoint = reader.ReadUInt32();

                    //Readoffsets(spearlevelbox, 1094, 2);
                    //Readoffsets(spearpointbox, 1100, 4);
                    //Readoffsets(longspearlevelbox, 1178, 2);
                    //Readoffsets(longspearpointbox, 1184, 4);
                    //Readoffsets(horseslevelbox, 1262, 2);
                    //Readoffsets(horsespointbox, 1268, 4);
                    //Readoffsets(halberdslevelbox, 1346, 2);
                    //Readoffsets(halberdspointsbox, 1352, 4);
                    //Readoffsets(Axeslevelbox, 1430, 2);
                    //Readoffsets(axepointbox, 1436, 4);
                    //Readoffsets(Clubslevelboxes, 1514, 2);
                    //Readoffsets(clubpointbox, 1520, 4);
                    //Readoffsets(Bowlevelbox, 1598, 2);
                    //Readoffsets(bowpointbox, 1604, 4);
                    //Readoffsets(horsebowlevel, 1682, 2);
                    //Readoffsets(horsebowpointbox, 1688, 4);
                    //Readoffsets(Camellevelbox, 1766, 2);
                    //Readoffsets(camelpointbox, 1772, 4);
                    //Readoffsets(elephantlevelbox, 1850, 2);
                    //Readoffsets(elephantpointbox, 1856, 4);
                    //Readoffsets(chariotlevelbox, 1934, 2);
                    //Readoffsets(chariotpointbox, 1940, 4);
                    //Readoffsets(explosivelevelbox, 2018, 2);
                    //Readoffsets(explosivepointbox, 2024, 4);
                    //Readoffsets(magiclevelbox, 2102, 2);
                    //Readoffsets(magicpointbox, 2108, 4);
                    //Readoffsets(engineerlevelbox, 2186, 2);
                    //Readoffsets(engineerpointbox, 2192, 4);
                    LoadSlot(null, null);
                    SaveSlot.SelectionChanged += LoadSlot;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                    SetStatus("ERROR!");
                    return;
                }
                SetStatus("Loaded!");
                reader.Flush();
            }
        }

        private void LoadSlot(object sender, SelectionChangedEventArgs e)
        {
            ////Save
            //Slot[SaveSlot.SelectedIndex].Name = CharName.Text;
            //Slot[SaveSlot.SelectedIndex].Money = MoneyBox.Value;
            //Slot[SaveSlot.SelectedIndex].Knivelv = knives.Levelbox.Value;
            //Slot[SaveSlot.SelectedIndex].Knivepoint = knives.PointBox.Value;

            ////Load
            CharName.Text = Slot[SaveSlot.SelectedIndex].Name;
            //MoneyBox.Value = Slot[SaveSlot.SelectedIndex].Money;
            knives.Levelbox.Value = Slot[SaveSlot.SelectedIndex].Knivelv;
            knives.PointBox.Value = Slot[SaveSlot.SelectedIndex].Knivepoint;
            MoneyBox.DataContext = Slot[SaveSlot.SelectedIndex].Money;
        }

        public void ReadID()
        {
            //Profile ID
            PID.Dispatcher.Invoke(new Action(() => PID.Text = _stfs.HeaderData.ProfileID));

            //Console ID
            CID.Dispatcher.Invoke(new Action(() => CID.Text = _stfs.HeaderData.ConsoleID));

            //Title ID - Smallbug if not substringed
            GID.Dispatcher.Invoke(new Action(() => GID.Text = _stfs.HeaderData.TitleID.Substring(8)));

            //Device ID
            DID.Dispatcher.Invoke(new Action(() => DID.Text = _stfs.HeaderData.DeviceID));

            //Save Name
            ContentBox.Dispatcher.Invoke(new Action(() => ContentBox.Text = _stfs.HeaderData.DisplayName));
        }

        #endregion Input

        #region Output

        #endregion Output

        #region Utilities

        private void MaxLevelBtnClick(object sender, RoutedEventArgs e)
        {
            {
                foreach (SquadControl squad in (Squads1.Children.OfType<SquadControl>()))
                {
                    squad.MaxLevel(null, null);
                }
                foreach (SquadControl squad in (Squads2.Children.OfType<SquadControl>()))
                {
                    squad.MaxLevel(null, null);
                }
                foreach (SquadControl squad in (Squads3.Children.OfType<SquadControl>()))
                {
                    squad.MaxLevel(null, null);
                }
            }
        }

        private void MaxPointsBtnClick(object sender, RoutedEventArgs e)
        {
            {
                foreach (SquadControl squad in (Squads1.Children.OfType<SquadControl>()))
                {
                    squad.MaxPoints(null, null);
                }
                foreach (SquadControl squad in (Squads2.Children.OfType<SquadControl>()))
                {
                    squad.MaxPoints(null, null);
                }
                foreach (SquadControl squad in (Squads3.Children.OfType<SquadControl>()))
                {
                    squad.MaxPoints(null, null);
                }
            }
        }

        private void MaxMoneyClick(object sender, RoutedEventArgs e)
        {
            MoneyBox.Value = 0x00FFFFFF;
        }

        private void ExitButtonClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void AllcheckClick(object sender, RoutedEventArgs e)
        {
            foreach (CheckBox check in checkwrap.Children.OfType<CheckBox>())
            {
                check.IsChecked = check.IsChecked != true;
            }
        }

        private void SetStatus(string text)
        {
            if (status.Dispatcher.CheckAccess())
                status.Content = ("Status: " + text);
            else
            {
                status.Dispatcher.Invoke(delegate { status.Content = ("Status: " + text); });
            }
        }

        #region ID Management

        private void NullidbtnClick(object sender, RoutedEventArgs e)
        {
            PID.Text = "E000000000000000";
            DID.Text = "0000000000000000000000000000000000000000";
            CID.Text = "0000000000";
        }

        private void RestoreidbtnClick(object sender, RoutedEventArgs e)
        {
            ReadID();
        }

        private void TransferidbtnClick(object sender, RoutedEventArgs e)
        {
        }

        #endregion

        #endregion Utilities

        //protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        //{
        //    base.OnMouseLeftButtonDown(e);

        //    // Begin dragging the window
        //    DragMove();
        //}

        private void OpenButtonClick(object sender, RoutedEventArgs e)
        {
            OpenFile();
        }
    }
}