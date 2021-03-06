﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace NiceHashMiner
{
    public partial class SubmitResultDialog : Form
    {
        private bool InBenchmark;
        private int Time, TimeIndex, DeviceChecked_Index;
        private string CurrentAlgoName;
        private Miner mm;

        public SubmitResultDialog(int tI)
        {
            InitializeComponent();
            TimeIndex = tI;
            InBenchmark = false;
            DeviceChecked_Index = 0;
            mm = null;

            for (int i = 0; i < Form1.Miners.Length; i++)
            {
                for (int j = 0; j < Form1.Miners[i].CDevs.Count; j++)
                {
                    ComputeDevice D = Form1.Miners[i].CDevs[j];

                    ListViewItem lvi = new ListViewItem(D.Vendor);
                    lvi.SubItems.Add(D.Name);
                    lvi.SubItems.Add(D.ID.ToString());
                    lvi.Tag = Form1.Miners[i];
                    DevicesListView.Items.Add(lvi);
                }
            }
        }

        private void CloseBtn_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void BenchmarkBtn_Click(object sender, EventArgs e)
        {
            if (InBenchmark)
            {
                mm.BenchmarkSignalQuit = true;
                InBenchmark = false;
                index = 9999;
                return;
            }

            bool DeviceChecked = false;
            string DeviceName = "";

            for (int i = 0; i < DevicesListView.Items.Count; i++)
            {
                if (DevicesListView.Items[i].Selected)
                {
                    DeviceChecked = true;
                    Int32.TryParse(DevicesListView.Items[i].SubItems[2].Text, out DeviceChecked_Index);

                    mm = DevicesListView.Items[i].Tag as Miner;
                    DeviceName = DevicesListView.Items[i].SubItems[1].Text;
                }
            }

            if (!DeviceChecked)
            {
                MessageBox.Show("Please choose at least one device to submit the results.", "No device selected",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            InBenchmark = true;
            DevicesListView.Enabled = false;
            CloseBtn.Enabled = false;
            BenchmarkBtn.Text = "Stop";
            LabelProgressPercentage.Text = "0.00%";
            index = 0;

            Helpers.ConsolePrint("SubmitResultDialog", "Number of Devices: " + mm.CDevs.Count);
            if (mm.CDevs.Count == 1 && mm.CountBenchmarkedAlgos() != 0)
            {
                DialogResult result = MessageBox.Show("You could use previously benchmarked values. Choose Yes to use previously benchmarked " +
                                                      "values or choose No to re-run the benchmark?", "Use previously benchmarked values?",
                                                      MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (result == System.Windows.Forms.DialogResult.Yes) index = 9999;
            }

            // Temporarily disable the other ComputeDevices in the same Group
            for (int i = 0; i < mm.CDevs.Count; i++)
            {
                if (mm.CDevs[i].ID != DeviceChecked_Index)
                    mm.CDevs[i].Enabled = false;
                else
                    mm.CDevs[i].Enabled = true;
            }

            BenchmarkProgressBar.Maximum = mm.SupportedAlgorithms.Length;

            // Parse GPU name
            Helpers.ConsolePrint("SubmitResultDialog", "Old DeviceName: " + DeviceName);
            if (DeviceName.Contains("GeForce") || DeviceName.Contains("GTX") || DeviceName.Contains("GT"))
            {
                string [] DeviceNameSplit = DeviceName.Split(' ');

                for (int i = 0; i < DeviceNameSplit.Length; i++)
                {
                    Helpers.ConsolePrint("DEBUG", "DeviceNameSplit[" + i + "]: " + DeviceNameSplit[i]);

                    if (DeviceNameSplit[i].Equals("GT") || DeviceNameSplit[i].Equals("GTX"))
                    {
                        if ((i + 2) <= DeviceNameSplit.Length)
                        {
                            DeviceName = "NVIDIA " + DeviceNameSplit[i] + DeviceNameSplit[i + 1];
                            for (int j = i + 2; j < DeviceNameSplit.Length; j++)
                            {
                                DeviceName += " " + DeviceNameSplit[j];
                            }

                            break;
                        }
                    }
                }
            }
            Helpers.ConsolePrint("SubmitResultDialog", "New DeviceName: " + DeviceName);

            url = "https://www.nicehash.com/?p=calc&name=" + DeviceName;
            InitiateBenchmark();
        }

        private int index;
        private string url;

        private void InitiateBenchmark()
        {
            int algoIndex = index;
            index++;

            if (algoIndex < mm.SupportedAlgorithms.Length)
            {
                if (!mm.SupportedAlgorithms[algoIndex].Skip)
                {
                    if (mm is cpuminer)
                        Time = Config.ConfigData.BenchmarkTimeLimitsCPU[TimeIndex];
                    else if (mm is ccminer)
                        Time = Config.ConfigData.BenchmarkTimeLimitsNVIDIA[TimeIndex];
                    else
                    {
                        Time = Config.ConfigData.BenchmarkTimeLimitsAMD[TimeIndex] / 60;

                        // add an aditional minute if second is not 0
                        if (DateTime.Now.Second != 0)
                            Time += 1;
                    }

                    CurrentAlgoName = mm.SupportedAlgorithms[algoIndex].NiceHashName;
                    UpdateProgressBar(false);
                    mm.BenchmarkStart(algoIndex, Time, BenchmarkCompleted, DevicesListView.Items[DeviceChecked_Index].Tag);
                }
                else
                {
                    UpdateProgressBar(true);
                    InitiateBenchmark();
                }
            }
            else
            {
                double[] div = { 1000000, // Scrypt MH/s
                                 1000000000000, // SHA256 TH/s
                                 1000000, // ScryptNf MH/s
                                 1000000, // X11
                                 1000000, // X13
                                 1000000, // Keccak
                                 1000000, // X15
                                 1000000, // Nist5
                                 1000000, // NeoScrypt
                                 1000000, // Lyra2RE
                                 1000000, // WhirlpoolX
                                 1000000, // Qubit
                                 1000000, // Quark
                                 1000,    // Axiom
                                 1000000, // Lyra2REv2
                                 1000,    // ScryptJaneNf16
                                 1000000000, // Blake256r8
                                 1000000000, // Blake256r14
                                 1000000000, // Blake256r8vnl
                                 1000000 };  // Ethereum

                for (int i = 0; i < mm.SupportedAlgorithms.Length; i++)
                {
                    if (!mm.SupportedAlgorithms[i].Skip)
                    {
                        int id = mm.SupportedAlgorithms[i].NiceHashID;
                        if (!mm.SupportedAlgorithms[i].NiceHashName.Equals("ethereum"))
                        {
                            url += "&speed" + id + "=" + (mm.SupportedAlgorithms[i].BenchmarkSpeed / div[id]).ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                        }
                        else
                        {
                            url += "&speedeth=" + (mm.SupportedAlgorithms[i].BenchmarkSpeed / div[id]).ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                        }
                    }
                }

                InBenchmark = false;
                DevicesListView.Enabled = true;
                CloseBtn.Enabled = true;
                BenchmarkBtn.Text = "Start";
                BenchmarkProgressBar.Value = 0;
                LabelProgressPercentage.Text = "Completed!";

                if (mm.BenchmarkSignalQuit)
                {
                    LabelProgressPercentage.Text = "Stopped!";
                    return;
                }

                System.Diagnostics.Process.Start(url);
            }
        }

        private void BenchmarkCompleted(bool success, string text, object tag)
        {
            if (this.InvokeRequired)
            {
                UpdateProgressBar(true);
                BenchmarkComplete d = new BenchmarkComplete(BenchmarkCompleted);
                this.Invoke(d, new object[] { success, text, tag });
            }
            else
            {
                InitiateBenchmark();
            }
        }

        private void UpdateProgressBar(bool step)
        {
            if (step) BenchmarkProgressBar.PerformStep();
            LabelProgressPercentage.Text = ((double)((double)BenchmarkProgressBar.Value / (double)BenchmarkProgressBar.Maximum) * 100).ToString("F2") + "%" + 
                                           " : Benchmarking " + CurrentAlgoName + ".. Please wait..";
        }
    }
}
