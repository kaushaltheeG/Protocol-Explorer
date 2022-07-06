
namespace OmegaScript.Scripts
{
    partial class disruptProfileKey
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(disruptProfileKey));
            this.label1 = new System.Windows.Forms.Label();
            this.keyInfoTextBox = new System.Windows.Forms.TextBox();
            this.backBtn = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.125F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(225, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(297, 31);
            this.label1.TabIndex = 0;
            this.label1.Text = "Key: Disruption Profiles";
            // 
            // keyInfoTextBox
            // 
            this.keyInfoTextBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.keyInfoTextBox.Location = new System.Drawing.Point(44, 43);
            this.keyInfoTextBox.Multiline = true;
            this.keyInfoTextBox.Name = "keyInfoTextBox";
            this.keyInfoTextBox.ReadOnly = true;
            this.keyInfoTextBox.Size = new System.Drawing.Size(714, 364);
            this.keyInfoTextBox.TabIndex = 1;
            this.keyInfoTextBox.Text = resources.GetString("keyInfoTextBox.Text");
            // 
            // backBtn
            // 
            this.backBtn.Location = new System.Drawing.Point(12, 413);
            this.backBtn.Name = "backBtn";
            this.backBtn.Size = new System.Drawing.Size(77, 37);
            this.backBtn.TabIndex = 2;
            this.backBtn.Text = "<<<";
            this.backBtn.UseVisualStyleBackColor = true;
            this.backBtn.Click += new System.EventHandler(this.backBtn_Click);
            // 
            // disruptProfileKey
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(12F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(806, 458);
            this.Controls.Add(this.backBtn);
            this.Controls.Add(this.keyInfoTextBox);
            this.Controls.Add(this.label1);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.875F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "disruptProfileKey";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "disruptProfileKey";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox keyInfoTextBox;
        private System.Windows.Forms.Button backBtn;
    }
}