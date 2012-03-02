using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace SyncVideo
{
    public partial class PlayerForm : Form
    {
        private ConfigForm _config;

        public bool PlayerClosing;

        public PlayerForm()
        {
            InitializeComponent();            
            _config = new ConfigForm(this);
            _config.Show();

            Closed += (x, y) =>
                          {
                              if (PlayerClosing)
                                  return;
                              PlayerClosing = true;
                              _config.Close();
                          };
        }

        public bool ExpectingStateChange = false;

        private void MediaControl_PlayStateChange(object sender, AxWMPLib._WMPOCXEvents_PlayStateChangeEvent e)
        {
            if (_config == null)
                return;
            if(ExpectingStateChange)
            {
                return;
            }
            _config.SyncState();
        }

        public bool ExpectingPositionChange = false;

        private void MediaControl_PositionChange(object sender, AxWMPLib._WMPOCXEvents_PositionChangeEvent e)
        {
            if (_config == null)
                return;
            if (ExpectingPositionChange)
            {
                return;
            }
            _config.SyncState();
        }
    }
}
