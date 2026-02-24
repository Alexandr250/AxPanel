using AxPanel.UI.Themes;
using AxPanel.UI.UserControls;

namespace AxPanelWinFormsTest;

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
        AxPanel.GridLayoutEngine gridLayoutEngine1 = new AxPanel.GridLayoutEngine();
        buttonContainerView1 = new ButtonContainerView();
        SuspendLayout();
        // 
        // buttonContainerView1
        // 
        buttonContainerView1.Anchor =      AnchorStyles.Top  |  AnchorStyles.Bottom   |  AnchorStyles.Left   |  AnchorStyles.Right ;
        buttonContainerView1.Arguments = null;
        buttonContainerView1.BackColor = SystemColors.ActiveCaption;
        buttonContainerView1.BaseControlPath = null;
        buttonContainerView1.DownloadUrl = null;
        buttonContainerView1.IsArchive = false;
        buttonContainerView1.IsWaitingForExpand = false;
        gridLayoutEngine1.Gap = 3;
        buttonContainerView1.LayoutEngine = gridLayoutEngine1;
        buttonContainerView1.Location = new Point( 12, 12 );
        buttonContainerView1.Name = "buttonContainerView1";
        buttonContainerView1.PanelName = null;
        buttonContainerView1.Size = new Size( 478, 553 );
        buttonContainerView1.TabIndex = 0;
        buttonContainerView1.Text = "buttonContainerView1";
        // 
        // Form1
        // 
        AutoScaleDimensions = new SizeF( 7F, 15F );
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size( 502, 577 );
        Controls.Add( buttonContainerView1 );
        KeyPreview = true;
        Name = "Form1";
        Text = "Form1";
        KeyDown +=  Form1_KeyDown ;
        KeyUp +=  Form1_KeyUp ;
        ResumeLayout( false );
    }

    #endregion

    private ButtonContainerView buttonContainerView1;
}
