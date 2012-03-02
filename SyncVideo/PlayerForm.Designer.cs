namespace SyncVideo
{
    partial class PlayerForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PlayerForm));
            this.MediaControl = new AxWMPLib.AxWindowsMediaPlayer();
            ((System.ComponentModel.ISupportInitialize)(this.MediaControl)).BeginInit();
            this.SuspendLayout();
            // 
            // MediaControl
            // 
            this.MediaControl.Dock = System.Windows.Forms.DockStyle.Fill;
            this.MediaControl.Enabled = true;
            this.MediaControl.Location = new System.Drawing.Point(0, 0);
            this.MediaControl.Name = "MediaControl";
            this.MediaControl.OcxState = ((System.Windows.Forms.AxHost.State)(resources.GetObject("MediaControl.OcxState")));
            this.MediaControl.Size = new System.Drawing.Size(624, 442);
            this.MediaControl.TabIndex = 0;
            this.MediaControl.PlayStateChange += new AxWMPLib._WMPOCXEvents_PlayStateChangeEventHandler(this.MediaControl_PlayStateChange);
            this.MediaControl.PositionChange += new AxWMPLib._WMPOCXEvents_PositionChangeEventHandler(this.MediaControl_PositionChange);
            // 
            // Player
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.ClientSize = new System.Drawing.Size(624, 442);
            this.Controls.Add(this.MediaControl);
            this.MaximizeBox = false;
            this.Name = "Player";
            this.Text = "Media Playback";
            ((System.ComponentModel.ISupportInitialize)(this.MediaControl)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        public AxWMPLib.AxWindowsMediaPlayer MediaControl;

    }
}

