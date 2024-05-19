namespace Chubrik.Grapher
{
    internal partial class Form
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            PictureBox = new PictureBox();
            ((System.ComponentModel.ISupportInitialize)PictureBox).BeginInit();
            SuspendLayout();
            // 
            // PictureBox
            // 
            PictureBox.BackColor = Color.Black;
            PictureBox.Dock = DockStyle.Fill;
            PictureBox.Location = new Point(0, 0);
            PictureBox.Name = "PictureBox";
            PictureBox.Size = new Size(800, 450);
            PictureBox.TabIndex = 0;
            PictureBox.TabStop = false;
            PictureBox.MouseDown += PictureBox_MouseDown;
            PictureBox.MouseLeave += PictureBox_MouseLeave;
            PictureBox.MouseMove += PictureBox_MouseMove;
            PictureBox.MouseUp += PictureBox_MouseUp;
            PictureBox.MouseWheel += PictureBox_Wheel;
            // 
            // Form
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(PictureBox);
            Name = "Form";
            Text = "Grapher";
            WindowState = FormWindowState.Maximized;
            ResizeEnd += Form_ResizeEnd;
            KeyDown += Form_KeyDown;
            KeyUp += Form_KeyUp;
            Resize += Form_Resize;
            ((System.ComponentModel.ISupportInitialize)PictureBox).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.PictureBox PictureBox;
    }
}

