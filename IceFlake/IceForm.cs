﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using IceFlake.Client;
using IceFlake.Client.Objects;
using IceFlake.Client.Patchables;
using IceFlake.Client.Scripts;
using IceFlake.DirectX;

namespace IceFlake
{
    public partial class IceForm : Form, ILog
    {
        public IceForm()
        {
            InitializeComponent();
        }

        #region ILog Members

        public void WriteLine(LogEntry entry)
        {
            Color logColor;
            switch (entry.Type)
            {
                case LogType.Error:
                    logColor = Color.Red;
                    break;
                case LogType.Warning:
                    logColor = Color.OrangeRed;
                    break;
                case LogType.Information:
                    logColor = Color.SteelBlue;
                    break;
                case LogType.Good:
                    logColor = Color.ForestGreen;
                    break;
                case LogType.Normal:
                default:
                    logColor = Color.Black;
                    break;
            }

            rbLogBox.Invoke((Action)(() =>
            {
                rbLogBox.SelectionColor = logColor;
                rbLogBox.AppendText(entry.FormattedMessage + Environment.NewLine);
                rbLogBox.ScrollToCaret();
            }));
        }

        #endregion

        private void IceForm_Load(object sender, EventArgs e)
        {
            Log.AddReader(this);
            Log.WriteLine("CoreForm loaded");

            SetupScripts();
        }

        private void IceForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            foreach (var s in Manager.Scripts.Scripts.Where(x => x.IsRunning))
                s.Stop();
        }

        private void btnDump_Click(object sender, EventArgs e)
        {
            try
            {
                if (Manager.LocalPlayer.Class != WoWClass.Shaman)
                    return;
                var healingWave = Manager.Spellbook["Healing Wave"];
                if (healingWave == null || !healingWave.IsValid)
                    return;
                Manager.ExecutionQueue.AddExececution(() =>
                {
                    healingWave.Cast();
                });
            }
            catch { }
        }

        #region Scripts tab

        private void SetupScripts()
        {
            if (Manager.Scripts == null)
                return;

            Manager.Scripts.ScriptRegistered += OnScriptRegisteredEvent;

            foreach (var s in Manager.Scripts.Scripts)
            {
                s.OnStartedEvent += OnScriptStartedEvent;
                s.OnStoppedEvent += OnScriptStoppedEvent;
            }

            lstScripts.DataSource = Manager.Scripts.Scripts.OrderBy(x => x.Category).ToList();
            Log.WriteLine(LogType.Information, "Loaded {0} scripts.", Manager.Scripts.Scripts.Count);            
        }

        private Script SelectedScript;

        private void btnScriptStart_Click(object sender, EventArgs e)
        {
            if (SelectedScript == null)
                return;

            SelectedScript.Start();
        }

        private void btnScriptStop_Click(object sender, EventArgs e)
        {
            if (SelectedScript == null)
                return;

            SelectedScript.Stop();
        }

        private void btnScriptCompile_Click(object sender, EventArgs e)
        {
            Manager.Scripts.CompileAsync();
        }

        private void lstScripts_SelectedIndexChanged(object sender, EventArgs e)
        {
            var script = lstScripts.SelectedItem as Script;
            if (script == null)
                return;

            SelectedScript = script;
        }

        private void lstScripts_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            var script = lstScripts.Items[e.Index] as Script;
            if (e.NewValue == CheckState.Checked)
                script.Start();
            else if (e.NewValue == CheckState.Unchecked)
                script.Stop();
        }

        private void OnScriptRegisteredEvent(object sender, EventArgs e)
        {
            Invoke((Action)(() =>
            {
                var script = (Script)sender;
                script.OnStartedEvent += new EventHandler(OnScriptStartedEvent);
                script.OnStoppedEvent += new EventHandler(OnScriptStoppedEvent);
                lstScripts.DataSource = Manager.Scripts.Scripts.OrderBy(x => x.Category).ToList();
            }));
            lstScripts.Invalidate();
        }

        private void OnScriptStartedEvent(object sender, EventArgs e)
        {
            Invoke((Action)(() =>
            {
                var idx = lstScripts.Items.IndexOf(sender);
                lstScripts.SetItemCheckState(idx, CheckState.Checked);
            }));
        }

        private void OnScriptStoppedEvent(object sender, EventArgs e)
        {
            Invoke((Action)(() =>
            {
                var idx = lstScripts.Items.IndexOf(sender);
                lstScripts.SetItemCheckState(idx, CheckState.Unchecked);
            }));
        }

        #endregion

        private void GUITimer_Tick(object sender, EventArgs e)
        {
            if (Manager.Scripts.Scripts.Where(s => s.IsRunning).Contains(SelectedScript))
            {
                btnScriptStart.Enabled = false;
                btnScriptStop.Enabled = true;
            }
            else
            {
                btnScriptStart.Enabled = true;
                btnScriptStop.Enabled = false;
            }

            if (!Manager.ObjectManager.IsInGame)
                return;

            var lp = Manager.LocalPlayer;
            lblHealth.Text = string.Format("{0}/{1} ({2:0}%)", lp.Health, lp.MaxHealth, lp.HealthPercentage);
            lblPowerText.Text = string.Format("{0}:", lp.PowerType);
            lblPower.Text = string.Format("{0}/{1} ({2:0}%)", lp.Power, lp.MaxPower, lp.PowerPercentage);
            lblLevel.Text = string.Format("{0}", lp.Level);
            lblZone.Text = string.Format("{0} ({1})", World.CurrentZone, World.CurrentSubZone);
        }

        private void btnExecute_Click(object sender, EventArgs e)
        {
            var lua = tbLUA.Text;
            if (string.IsNullOrEmpty(lua))
                return;
            Manager.ExecutionQueue.AddExececution(() =>
            {
                Log.WriteLine(lua);
                var ret = WoWScript.Execute(lua);
                for (var i = 0; i < ret.Count; i++)
                    Log.WriteLine("\t[{0}] = \"{1}\"", i, ret[i]);
            });
        }
    }
}
