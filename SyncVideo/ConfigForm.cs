using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using WMPLib;

namespace SyncVideo
{
    public partial class ConfigForm : Form
    {
        private SyncServer _server;
        private SyncClient _client;
        private PlayerForm _player;
        private SyncExecutionContext _context = new SyncExecutionContext();

        public bool Server
        {
            get { return rbServer.Checked; }
        }

        private void UpdateVisibility()
        {
            textBox1.Enabled = !Server;
            if(Server)
            {
                if(_server != null)
                {
                    buttonStart.Enabled = false;
                    button2.Enabled = buttonStop.Enabled = true;                    
                }
                else
                {
                    buttonStart.Enabled = true;
                    button2.Enabled = buttonStop.Enabled = false;
                }
            }
            else
            {
                if(_client != null)
                {
                    buttonStart.Enabled = false;
                    button2.Enabled = buttonStop.Enabled = true;
                }
                else
                {
                    buttonStart.Enabled = true;
                    button2.Enabled = buttonStop.Enabled = false;
                }
            }
            rbClient.Enabled = rbServer.Enabled = buttonStart.Enabled;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Winapi)]
        internal static extern IntPtr GetFocus();

        private Control GetFocusedControl()
        {
            Control focusedControl = null;
            // To get hold of the focused control:
            IntPtr focusedHandle = GetFocus();
            if (focusedHandle != IntPtr.Zero)
                // Note that if the focused Control is not a .Net control, then this will return null.
                focusedControl = Control.FromHandle(focusedHandle);
            return focusedControl;
        }

        public void NetLog(string s)
        {
            connectionLog.Text += s + "\r\n";
            var focus = GetFocusedControl();
            connectionLog.Focus();
            connectionLog.SelectionStart = connectionLog.Text.Length;
            connectionLog.ScrollToCaret();
            if (focus != null)
                focus.Focus();
        }

        public ConfigForm(PlayerForm player)
        {
            _player = player;
            Closed += (x, y) =>
            {
                if (_player.PlayerClosing)
                    return;
                _player.Close();                
            };

            InitializeComponent();
            _context.SetPlayPosition = x =>
                                           {
                                               _player.ExpectingPositionChange = true;
                                               _player.MediaControl.Ctlcontrols.currentPosition = x;
                                               _player.ExpectingPositionChange = false;
                                           };
            _context.SetPlayState = x =>
                                        {
                                            _player.ExpectingStateChange = true;
                                            switch(x)
                                            {
                                                case 1: //Stopped	Playback of the current media item is stopped.
                                                    _player.MediaControl.Ctlcontrols.stop();
                                                    break;
                                                case 2: //Paused	Playback of the current media item is paused. 
                                                    //When a media item is paused, resuming playback begins from the same location.
                                                    _player.MediaControl.Ctlcontrols.pause();
                                                    break;
                                                case 3: //Playing	The current media item is playing.
                                                    _player.MediaControl.Ctlcontrols.play();
                                                    break;
                                                case 4: //ScanForward	The current media item is fast forwarding.
                                                    _player.MediaControl.Ctlcontrols.fastForward();
                                                    break;
                                                case 5: //ScanReverse	The current media item is fast rewinding.
                                                    _player.MediaControl.Ctlcontrols.fastReverse();
                                                    break;                                                
                                            }
                                            _player.ExpectingStateChange = false;
                                        };
            _context.AttemptPlayFile = AttemptPlayFile;
            Action<string> netLog = NetLog;
            _context.Log = x => this.Invoke(netLog, x);
            _context.GetSyncMessage = GetSyncMessage;

            UpdateVisibility();
            rbServer.CheckedChanged += (x,y) => UpdateVisibility();
            rbClient.CheckedChanged += (x, y) => UpdateVisibility();

            textBox1.Text = Properties.Settings.Default.ServerIP;
        }

        public SyncStateMessage GetSyncMessage()
        {
            int playState = 0;
            switch (_player.MediaControl.playState)
            {
                case WMPPlayState.wmppsStopped:
                    playState = 1;
                    break;
                case WMPPlayState.wmppsPaused:
                    playState = 2;
                    break;
                case WMPPlayState.wmppsPlaying:
                    playState = 3;
                    break;
                case WMPPlayState.wmppsScanForward:
                    playState = 4;
                    break;
                case WMPPlayState.wmppsScanReverse:
                    playState = 5;
                    break;
            }
            return new SyncStateMessage(playState, _player.MediaControl.Ctlcontrols.currentPosition);
        }

        public void SyncState()
        {
            if(Server)
            {
                if (_server == null)
                    return;

                _server.PropogateMessage(null);
            }
            else
            {
                if (_client == null)
                    return;

                _client.PropogateToServer(GetSyncMessage());                
            }
        }

        public void KillConnection()
        {
            if (Server && _server != null)
            {
                _server.Stop();
                _server = null;
            }
            else if (!Server && _client != null)
            {
                _client.Stop();
                _client = null;
            }
        }

        private void ConfigForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            KillConnection();
        }

        private void AttemptPlayFile(string fileName)
        {
            if(_player.MediaControl.URL != null && _player.MediaControl.URL != "")
            {
                var file = new FileInfo(_player.MediaControl.URL);
                if (file.Name.ToLower() == fileName.ToLower())
                    return;
                var newFile = file.Directory.GetFiles().Where(x => x.Name == fileName).FirstOrDefault();
                if(newFile != null)
                {
                    _player.MediaControl.URL = newFile.FullName;
                }
            }
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Filter = "Video files (*.wmv *.avi)|*.wmv;*.avi|All files (*.*)|*.*";
            if(dialog.ShowDialog() == DialogResult.OK)
            {
                _player.MediaControl.URL = dialog.FileName;
                var file = new FileInfo(dialog.FileName);
                SendMessage(new SyncPlayFileMessage(file.Name));
            }
        }

        private void SendMessage(SyncClientMessage message)
        {
            if (Server)
            {
                if (_server != null && _server.Running)
                    _server.PropogateMessage(message);
            }
            else
            {
                if(_client != null && _client.Running)
                    _client.PropogateToServer(message);
            }
        }

        private void button2_Click_1(object sender, EventArgs e)
        {
            if(Server)
                _server.PropogateMessage(null);
            else
                _client.PropogateToServer(GetSyncMessage());
            UpdateVisibility();
        }

        private void buttonStart_Click(object sender, EventArgs e)
        {
            KillConnection();
            if(Server)
            {
                _server = new SyncServer(_context);
                _server.Start();
            }
            else
            {
                _client = new SyncClient(_context, textBox1.Text);
                _client.Start();
                Properties.Settings.Default.ServerIP = textBox1.Text;
                Properties.Settings.Default.Save();
            }
            UpdateVisibility();
        }

        private void buttonStop_Click(object sender, EventArgs e)
        {
            KillConnection();
            UpdateVisibility();
        }

        private void checkBox1_CheckStateChanged(object sender, EventArgs e)
        {
            _player.TopMost = checkBox1.Checked;
        }
    }
}
