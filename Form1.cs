using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace PackVideo
{
    public partial class Form1 : Form
    {
        private VideoConverter converter;
        private string Version = "1.1 2021";

        public Form1()
        {
            InitializeComponent();

            converter = new VideoConverter();
            converter.UpdateStatus = UpdateStatus;

            this.Text += " ver " + Version;

            tbLog.Text = @"Программа для пересжатия mp4
Версия " + Version + @"
Автор Aant

Программа пересжимает все mp4 файлы в avi с тем же именем, если таких файлов уже не существует.
Рядом с программой должен быть ffmpeg.exe.
"
            + Environment.NewLine + $"Параметры для сжатия: {converter.FfmpegArguments}"
            + Environment.NewLine + $"Параметры для сжатия с уменьшением размера: {converter.FfmpegArgumentsResize}"
            + @"


Copyright 2021 Ivanov Vasilii Sergeevich aka Aant
Licensed under the LGPLv2.1
https://github.com/AantCoder/PackVideo

Components used:
* FFmpeg (LGPLv2.1 license) https://ffmpeg.org/
";
        }

        private void UpdateStatus(bool showError)
        {
            if (showError) MessageBox.Show(converter.Status, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);

            if (this.InvokeRequired)
                this.Invoke((Action)UpdateStatusDo);
            else
                UpdateStatusDo();
        }

        private void UpdateStatusDo()
        {
            tbLog.Text = converter.Log;
            lStatus.Text = converter.Status;
            if (converter.ProgressText != null) this.Text = converter.ProgressText;
            progressBar.Value = converter.ProgressValue;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (tbFolder.Text == string.Empty)
            {
                if (folderBrowserDialog.ShowDialog() != DialogResult.OK) return;
                tbFolder.Text = folderBrowserDialog.SelectedPath;
            }
            button1.Enabled = true;
            button2.Enabled = false;
            tbFolder.ReadOnly = true;

            if (!converter.GetFiles(tbFolder.Text))
            {
                lStatus.Text = "Файлы для сжатия не найдены";
                tbLog.Text = "";
                button1.Enabled = false;
                button2.Enabled = true;
                tbFolder.Text = "";
                tbFolder.ReadOnly = false;
                return;
            }
            lStatus.Text = converter.Status;
            tbLog.Text = converter.Log;

        }

        private void button1_Click(object sender, EventArgs e)
        {
            button1.Enabled = false;
            cbDel.Enabled = false;
            cbAutoResize.Enabled = false;
            lStatus.Text = "Подготовка...";
            converter.Start(cbDel.Checked, cbAutoResize.Checked);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            converter.Stop();
        }



    }
}
