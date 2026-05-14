namespace WinFormsApp1
{
    partial class Form1
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
            barcodeButtonEdit1 = new ScanAndScale.Driver.BarcodeButtonEdit();
            ((System.ComponentModel.ISupportInitialize)barcodeButtonEdit1.Properties).BeginInit();
            SuspendLayout();
            // 
            // barcodeButtonEdit1
            // 
            barcodeButtonEdit1.Config = null;
            barcodeButtonEdit1.Location = new Point(203, 94);
            barcodeButtonEdit1.Name = "barcodeButtonEdit1";
            barcodeButtonEdit1.Properties.Buttons.AddRange(new DevExpress.XtraEditors.Controls.EditorButton[] { new DevExpress.XtraEditors.Controls.EditorButton() });
            barcodeButtonEdit1.Size = new Size(510, 20);
            barcodeButtonEdit1.TabIndex = 0;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(barcodeButtonEdit1);
            Name = "Form1";
            Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)barcodeButtonEdit1.Properties).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private ScanAndScale.Driver.BarcodeButtonEdit barcodeButtonEdit1;
    }
}
