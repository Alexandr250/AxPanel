using AxPanel.UI.Themes;
using AxPanel.UI.UserControls;

namespace AxPanelWinFormsTest;

partial class Form2
{
    /// <summary>
    /// Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    /// Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose( bool disposing )
    {
        if ( disposing && ( components != null ) )
        {
            components.Dispose();
        }
        base.Dispose( disposing );
    }

    #region Windows Form Designer generated code

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        axPanelButton1 = new AxPanel.UI.UserControls.LaunchButtonView( new DarkTheme() );
        SuspendLayout();
        // 
        // axPanelButton1
        // 
        axPanelButton1.Location = new Point( 12, 12 );
        axPanelButton1.Name = "axPanelButton1";
        axPanelButton1.Size = new Size( 147, 38 );
        axPanelButton1.TabIndex = 0;
        axPanelButton1.Text = "axPanelButton1";
        // 
        // Form2
        // 
        AutoScaleDimensions = new SizeF( 7F, 15F );
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size( 303, 236 );
        Controls.Add( axPanelButton1 );
        Name = "Form2";
        Text = "Form2";
        ResumeLayout( false );
    }

    #endregion

    private LaunchButtonView axPanelButton1;
}