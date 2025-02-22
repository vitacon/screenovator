﻿using System;
using System.Collections.Generic;
//using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
//using System.Web.UI.WebControls.WebParts;
using System.Xml.Serialization;

namespace Screenovator
{

    public partial class FormMain : Form
    {
        private string formname = "Screenovator";

        public Version version = System.Reflection.Assembly.GetEntryAssembly().GetName().Version;

        private string extension = ".scrxml";

        //string path_exe = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);    // cesta k .exe
        string path_initial = Directory.GetCurrentDirectory();

        private const string str_panel_scene = "panel_scene";
        private const string str_label_sceneorder = "label_sceneorder";
        private const string str_label_scenename = "label_scenename";
        private const string str_label_sceneheading = "label_sceneheading";
        private const string str_label_scenelength = "label_scenelength";

        // pozice okrajů jednotlivých typů odstavců
        public const int tab_speechstart = 10;
        public const int tab_character = 20;
        public const int tab_speechend = 52;
        public const int tab_end = 62;

        // zdá se, že Courier 12 má šířku 10 pixelů na znak
        public const int margin_zero = 5;              // default okraj
        public const int margin_speechstart = 105;
        public const int margin_parenthetical = 155;
        public const int margin_character = 205;
        public const int margin_speechend = 100;
        //const int margin_end = 625;

        public const string space_tab_speechstart = "          ";
        public const string space_tab_character = "                    ";

        public Screenplay screenplay = new Screenplay();

        Scene currentscene = null;             // pointer na aktuálě vybranou scénu v objektu screenplay
        Panel currentscenepanel = null;        // pointer na aktuálně vybraný panel

        Configuration conf;

        // print hack
        private Font printFont;
        private StreamReader streamToPrint;


        public FormMain()
        {
            InitializeComponent();

            // přidáme do hlavičky okna číslo verze
            formname += " " + version.Major + "." + version.Minor;
            Text = formname;

            // config
            conf = Configuration.LoadXMLConfig();

            // custom barvy
            const int custmax = 255;
            const int custmid = 240;
            const int custmin = 216;
            const int custtwo = 224;
            colorDialog.CustomColors = new int[] {
                                        ColorTranslator.ToOle(Color.FromArgb(custmax, custtwo, custtwo)),
                                        ColorTranslator.ToOle(Color.FromArgb(custtwo, custmax, custtwo)),
                                        ColorTranslator.ToOle(Color.FromArgb(custtwo, custtwo, custmax)),

                                        ColorTranslator.ToOle(Color.FromArgb(custmax, custmid, custmin)),
                                        ColorTranslator.ToOle(Color.FromArgb(custmid, custmin, custmax)),
                                        ColorTranslator.ToOle(Color.FromArgb(custmin, custmax, custmid)),

                                        ColorTranslator.ToOle(Color.FromArgb(custmax,custmin,custmid)),
                                        ColorTranslator.ToOle(Color.FromArgb(custmin,custmid,custmax)),
                                        ColorTranslator.ToOle(Color.FromArgb(custmid,custmax,custmin))
                                      };


            // nastavíme okno
            //this.SuspendLayout();
            this.WindowState = conf.windowstate;
            //this.Location = conf.location;      // tohle nějak nefunguje - zřejmě to něco to až po dokončení inicializace přepíše
            //this.ResumeLayout();

            //flowLayoutPanel_scenes_list.Height = splitContainer_scenes.Panel1.ClientSize.Height - flowLayoutPanel_scenes_options.Height - 6;

            // nastavení panelu se scénami
            checkBox_showname.Checked = conf.displayname;
            checkBox_showheading.Checked = conf.displayheading;
            comboBox_panelheight.SelectedIndex = conf.heighttype;

            // zkusíme načíst poslední soubor
            if (conf.reopenlatestfile)
            {
                // zatím začínáme natvrdo načtením souboru
                screenplay.filename = conf.latestfilename;
                LoadScreenplay(screenplay.filename + extension);
            }

            // pokud nemáme ani jednu scénu, tak zřejmě neprošlo žádné načtení (pokud soubor nebyl zmršený)
            if (screenplay.scenelist.Count == 0)
            {
                screenplay.filename = "untitled";
                AddNewScene();
            }

            // zkusíme vyvolat přepočet velikostí flow panelů
            flowLayoutPanel_scenes_list.Height = splitContainer_scenes.Panel1.ClientSize.Height - flowLayoutPanel_scenes_options.Height - 6;

            // aktualizujeme zobrazovací výšku panelů
            UpdateVisibilityAllNames();
            UpdateVisibilityAllHeadings();
            UpdateAllPanelHeights();

            // pokud je okno v normálním stavu, tak provedeme nějaké manévry
            if (WindowState == FormWindowState.Normal)
            {
                // pozice a velikost okna
                this.StartPosition = FormStartPosition.Manual;
                this.Location = conf.location;
                if (conf.size.Width > 100 && conf.size.Height > 100)
                    this.Size = conf.size;

                // velikost splitteru
                if (conf.splitterdistance >= 150)
                {
                    splitContainer_scenes.SplitterDistance = conf.splitterdistance;
                    conf.splitterdistance = 0;
                }
            }

            SelectScene(0);
        }

        void AddPanelOfScene(Scene newscene)
        // přidá panel založený na dané scéně
        {
            Panel newpanel = new Panel();
            newpanel.SuspendLayout();

            newpanel.AllowDrop = true;
            newpanel.Anchor = ((System.Windows.Forms.AnchorStyles)((AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right)));
            newpanel.BackColor = newscene.color;
            newpanel.Location = new System.Drawing.Point(3, 90);
            newpanel.Margin = new System.Windows.Forms.Padding(3, 3, 3, 0);
            newpanel.Name = str_panel_scene + newscene.order;
            // není to úplně perfektní (při startu je tam rozdíl pár pixelů), ale je to únosné
            newpanel.Size = new System.Drawing.Size(splitContainer_scenes.Panel1.Width - 20, 39);
            newpanel.TabIndex = 4;
            newpanel.DragDrop += new System.Windows.Forms.DragEventHandler(panel_scene_DragDrop);
            newpanel.DragEnter += new System.Windows.Forms.DragEventHandler(panel_scene_DragEnter);
            newpanel.MouseDown += new System.Windows.Forms.MouseEventHandler(panel_scene_MouseDown);

            // label_sceneorder
            Label sceneorder = new Label();
            sceneorder.Location = new System.Drawing.Point(3, 3);
            sceneorder.Name = str_label_sceneorder + newscene.order;
            sceneorder.Size = new System.Drawing.Size(25, 14);
            sceneorder.Text = newscene.order.ToString();
            sceneorder.TextAlign = System.Drawing.ContentAlignment.TopRight;
            sceneorder.Cursor = Cursors.SizeNS;
            sceneorder.MouseDown += new System.Windows.Forms.MouseEventHandler(label_parentscene_MouseDown);
            newpanel.Controls.Add(sceneorder);

            // label_scenename
            Label scenename = new Label();
            scenename.Anchor = (AnchorStyles)(AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right);
            scenename.BackColor = System.Drawing.Color.FromArgb(192, 255, 255, 255);
            scenename.Location = new System.Drawing.Point(30, 0);
            scenename.Padding = new Padding(3, 1, 3, 3);
            scenename.TextAlign = ContentAlignment.MiddleLeft;
            scenename.MinimumSize = new System.Drawing.Size(20, 20);
            scenename.Name = str_label_scenename + newscene.order;
            scenename.Size = new System.Drawing.Size(newpanel.Width - 60, 20);
            scenename.Text = newscene.name;
            scenename.MouseDown += new System.Windows.Forms.MouseEventHandler(label_parentscene_MouseDown);
            newpanel.Controls.Add(scenename);

            // label_sceneheading
            Label sceneheading = new Label();
            sceneheading.Anchor = (AnchorStyles)(AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right);
            //sceneheading.BackColor = System.Drawing.Color.FromArgb(192, 255, 255, 255);
            sceneheading.Location = new System.Drawing.Point(30, 23);
            sceneheading.Padding = new Padding(3, 0, 3, 0);
            sceneheading.MinimumSize = new System.Drawing.Size(20, 13);
            sceneheading.Name = str_label_sceneheading + newscene.order;
            sceneheading.Size = new System.Drawing.Size(newpanel.Width - 60, 13);
            sceneheading.Text = newscene.heading;
            sceneheading.MouseDown += new System.Windows.Forms.MouseEventHandler(label_parentscene_MouseDown);
            newpanel.Controls.Add(sceneheading);

            // label_scenelength
            Label scenelength = new Label();
            scenelength.Anchor = (AnchorStyles)(AnchorStyles.Top | AnchorStyles.Right);
            scenelength.Location = new System.Drawing.Point(newpanel.Width - 26, 3);
            scenename.Name = str_label_scenelength + newscene.order;
            scenelength.Size = new System.Drawing.Size(36, 13);
            scenelength.Text = newscene.length.ToString();
            scenelength.TextAlign = System.Drawing.ContentAlignment.TopRight;
            scenelength.MouseDown += new System.Windows.Forms.MouseEventHandler(label_parentscene_MouseDown);
            newpanel.Controls.Add(scenelength);

            // přidáme na flowpanel
            flowLayoutPanel_scenes_list.Controls.Add(newpanel);
            // ještě je třeba ho zařadit na konec, ale před přidávací políčko
            flowLayoutPanel_scenes_list.Controls.SetChildIndex(newpanel, newscene.order - 1);

            // uložíme si odkaz na panel
            newscene.panelscene = newpanel;

            // nastavíme sí zkratky na labels
            newscene.labelorder = sceneorder;
            newscene.labelscenename = scenename;
            newscene.labelsceneheading = sceneheading;
            newscene.labelscenelength = scenelength;

            // uvolníme pro překreslení či co
            newpanel.ResumeLayout(false);


        }

        void AddNewScene()
        {
            // přidání scény do seznamu
            Scene newscene = new Scene();
            newscene.name = "A new scene";
            newscene.order = screenplay.scenelist.Count + 1;
            screenplay.scenelist.Add(newscene);

            // přidání panelu
            AddPanelOfScene(newscene);
        }

        void DeleteAllScenes()
        {
            foreach (Scene scene in screenplay.scenelist)
            {
                // zrušíme panel
                scene.panelscene.Dispose();
            }
            // zrušíme celý seznam scén
            screenplay.scenelist.Clear();

            // jméno souboru a projektu
            screenplay.docname = "Untitled";
            screenplay.filename = "untitled";

            // upravíme hlavičku okna
            Text = formname + " - " + screenplay.filename;
        }

        void DeleteScene(Scene scene)
        {
            // pokud je scéna jen jedna, tak je třeba speciální přístup
            if (screenplay.scenelist.Count <= 1)
            {
                DeleteAllScenes();
                AddNewScene();
                SelectScene(0);
            }
            else
            {
                // smazání scény i jejího panelu
                // Control.Dispose(Boolean)
                // https://docs.microsoft.com/en-us/dotnet/api/system.windows.forms.control.dispose?view=netframework-4.8
                // Object.Finalize

                int id = scene.order - 2;

                // zrušíme panel
                scene.panelscene.Dispose();

                // zrušíme scénu ze seznamu
                screenplay.scenelist.Remove(scene);

                // přečíslujeme scény
                UpdateOrdering();

                // nastavíme novou aktivní scénu
                if (id < 0)
                    id = 0;
                SelectScene(id); // scene

            }
        }

        void UpdateOrdering()
        {
            // přanastaví číslování scén a panelů po nějaké změně (třeba po smazání scény)
            for (int i = 0; i < screenplay.scenelist.Count; i++)
            {
                int id = i + 1;
                // číslo scény
                screenplay.scenelist[i].order = id;

                // jména ovládacích prvků
                // ovšem pokud se volá při loadu, tak ještě nejsou nastavené pointery na panel a není možné do toho vrtat
                if (screenplay.scenelist[i].panelscene != null)
                {
                    screenplay.scenelist[i].panelscene.Name = str_panel_scene + id;
                    screenplay.scenelist[i].labelorder.Name = str_label_sceneorder + id;
                    screenplay.scenelist[i].labelorder.Text = id.ToString();
                    screenplay.scenelist[i].labelscenename.Name = str_label_scenename + id;
                    screenplay.scenelist[i].labelsceneheading.Name = str_label_sceneheading + id;
                    screenplay.scenelist[i].labelscenelength.Name = str_label_scenelength + id;
                }
            }
        }

        void LoadScreenplay(string filename)
        {
            try
            {
                System.Xml.Serialization.XmlSerializer reader = new System.Xml.Serialization.XmlSerializer(typeof(Screenplay));
                System.IO.StreamReader file = new System.IO.StreamReader(filename);
                screenplay = (Screenplay)reader.Deserialize(file);
                file.Close();

                // pokud je v souboru rozbité číslování, tak je třeba to seřadit
                screenplay.scenelist.Sort();

                // a změnit čísla
                UpdateOrdering();

                // připojíme si k tomu panely
                foreach (Scene scene in screenplay.scenelist)
                    AddPanelOfScene(scene);

                // upravíme hlavičku okna
                Text = formname + " - " + filename;

                // hlášení na dolní liště
                label_info.Text = "File loaded";

                // pro jistotu reset příznaku neuložených změn
                screenplay.unsaved = false;
            }
            catch (Exception exception)
            {
                label_info.Text = exception.Message;
            }
        }

        void SaveScreenplay(string filename)
        {
            try
            {
                var writer = new System.Xml.Serialization.XmlSerializer(typeof(Screenplay));
                var wfile = new System.IO.StreamWriter(filename);
                writer.Serialize(wfile, screenplay);
                wfile.Close();

                // upravíme hlavičku okna
                Text = formname + " - " + filename;

                // hlášení na dolní liště
                label_info.Text = "File saved";

                // reset příznaku neuložených změn
                screenplay.unsaved = false;
            }
            catch (Exception exception)
            {
                label_info.Text = exception.Message;
            }
        }

        private void richTextBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) // Tab
            {
                //richTextBox.SuspendLayout();

                // najdu řádek
                int linenum = richTextBox_scene.GetLineFromCharIndex(richTextBox_scene.SelectionStart);

                string line = "";

                // Note that you must never touch the Text or the Lines directly or all previous formatting gets messed up.
                // je třeba ošetřit kurzor na pozici pod posledním řádkem
                int spacecount = 0;
                if (linenum >= richTextBox_scene.Lines.Length)
                {
                    // řádek neexistuje
                    richTextBox_scene.AppendText("");
                }
                else
                {
                    // řádek existuje, tak si spočítáme úvodní mezery
                    line = richTextBox_scene.Lines[linenum];
                    spacecount = line.TakeWhile(Char.IsWhiteSpace).Count();
                }

                // posun začátku řádku
                if (spacecount < tab_speechstart)
                {
                    // nastavíme 10
                    richTextBox_scene.SelectionStart = richTextBox_scene.GetFirstCharIndexFromLine(linenum);
                    richTextBox_scene.SelectionLength = spacecount;
                    richTextBox_scene.SelectedText = space_tab_speechstart;
                }
                else if (spacecount < tab_character)
                {
                    // nastavíme 20
                    richTextBox_scene.SelectionStart = richTextBox_scene.GetFirstCharIndexFromLine(linenum);
                    richTextBox_scene.SelectionLength = spacecount;
                    richTextBox_scene.SelectedText = space_tab_character;
                }
                else // 20 a více
                {
                    // nastavíme 0
                    richTextBox_scene.SelectionStart = richTextBox_scene.GetFirstCharIndexFromLine(linenum);
                    richTextBox_scene.SelectionLength = spacecount;
                    richTextBox_scene.SelectedText = "";

                }

                //FormatAllText();

                //richTextBox.ResumeLayout();
            }
            else if (e.KeyCode == Keys.Enter)
            {
                // změna margin po enteru
                // jaké tam bylo odsazení?
                int previndent = richTextBox_scene.SelectionIndent;

                // pokud předcházelo jméno postavy nebo vsuvka, tak nastavíme řeč
                if (previndent >= margin_parenthetical)
                {
                    // vložíme enter a nastavíme pro nový řádek jiný margin
                    richTextBox_scene.SelectedText = Environment.NewLine;
                    richTextBox_scene.SelectionIndent = margin_speechstart;
                    e.Handled = true;
                }
                else if (previndent == margin_speechstart)
                {
                    // vložíme 2x enter a nastavíme pro nový řádek jiný margin
                    richTextBox_scene.SelectedText = Environment.NewLine + Environment.NewLine;
                    richTextBox_scene.SelectionIndent = margin_character;
                    e.Handled = true;
                }

                ColorAllText();
            }
            else if (e.KeyCode == Keys.Down || e.KeyCode == Keys.PageDown)
            {
                // nesnažíme se náhodou scrollovat za okraj scény?
                int currentline = richTextBox_scene.GetLineFromCharIndex(richTextBox_scene.SelectionStart);
                int lastline = richTextBox_scene.GetLineFromCharIndex(richTextBox_scene.TextLength);
                if (currentline >= lastline && currentscene.order < screenplay.scenelist.Count)
                {
                    // jo, takže zkusíme posun dál
                    DeselectScene();
                    SelectScene(currentscene.order);    // defacto +1
                    richTextBox_scene.Focus();
                    e.Handled = true;
                }
            }
            else if (e.KeyCode == Keys.Up || e.KeyCode == Keys.PageUp)
            {
                // nesnažíme se náhodou scrollovat za okraj scény?
                int currentline = richTextBox_scene.GetLineFromCharIndex(richTextBox_scene.SelectionStart);
                if (currentline <= 0 && currentscene.order > 1)
                {
                    // jo, takže zkusíme posun dál
                    DeselectScene();
                    SelectScene(currentscene.order - 2);    // defacto -1
                    richTextBox_scene.Focus();
                    richTextBox_scene.SelectionStart = richTextBox_scene.TextLength;
                    e.Handled = true;
                }
            }

        }

        private void richTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            // ošetříme, aby tab nevyskakoval jinam
            if (e.KeyChar == 9)
            {
                e.Handled = true;
            }

            else if (e.KeyChar == '(')
            {
                // zmáčknuta závorka - pokud je to první položka na řádku, tak nastavit margin pro vstuvku
                //*** zatím spíš hack
                // výpis pozice kurzoru
                int pos = richTextBox_scene.SelectionStart;
                int linestart = richTextBox_scene.GetFirstCharIndexOfCurrentLine();
                int linepos = pos - linestart;
                if (linepos == 0)
                {
                    // je to první znak na řádku, takže přeformátujeme řádek
                    richTextBox_scene.SelectionIndent = margin_parenthetical;
                    richTextBox_scene.SelectionRightIndent = margin_speechend;
                }
            }
        }

        private void richTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            UpdateCursorPosition();

            if (e.KeyCode == Keys.Tab)
            {
                // různé vzdálené tabulátorování
                if (richTextBox_scene.SelectionIndent < margin_speechstart)
                {
                    // speech

                    richTextBox_scene.SelectionIndent = margin_speechstart;
                    richTextBox_scene.SelectionRightIndent = margin_speechend;
                }
                else if (richTextBox_scene.SelectionIndent < margin_character)
                {
                    // character

                    richTextBox_scene.SelectionIndent = margin_character;
                    richTextBox_scene.SelectionRightIndent = margin_speechend;

                    // změníme na uppercase
                    //*** ideálně by to chtělo uložit si řádek v původní verzi, aby se k němu dalo vrátít
                    int pos = richTextBox_scene.SelectionStart;
                    int linenum = richTextBox_scene.GetLineFromCharIndex(richTextBox_scene.SelectionStart);
                    int linestart = richTextBox_scene.GetFirstCharIndexOfCurrentLine();
                    int lineend = richTextBox_scene.GetFirstCharIndexFromLine(linenum + 1);
                    int linelen = lineend - linestart - 1;
                    if (linelen < 0)
                        linelen = 100;  // opět pofiderní hack

                    richTextBox_scene.SelectionStart = linestart;
                    richTextBox_scene.SelectionLength = linelen;
                    richTextBox_scene.SelectedText = richTextBox_scene.SelectedText.ToUpper();
                    richTextBox_scene.SelectionStart = pos;
                }
                else
                {
                    // action

                    richTextBox_scene.SelectionIndent = margin_zero;
                    richTextBox_scene.SelectionRightIndent = margin_zero;
                }
            }
        }

        private void button_color_Click(object sender, EventArgs e)
        {
            // Show the color dialog.
            DialogResult result = colorDialog.ShowDialog();

            // See if user pressed ok.
            if (result == DialogResult.OK)
            {
                // Set form background to the selected color.
                panelHeader.BackColor = colorDialog.Color;

                // nastavíme barvu u panelu
                currentscenepanel.BackColor = colorDialog.Color;

                // nastavíme barvu do seznamu
                currentscene.color = colorDialog.Color;
            }
        }

        int GetLineType(string line)
        {
            // 0 action
            // 1 speech
            // 2 vsuvka
            // 3 character
            if (line.Length == 0)
                return 0;

            int spacecount = line.TakeWhile(Char.IsWhiteSpace).Count();
            if (spacecount >= tab_character)
            {
                return 3;
            }
            else if (spacecount >= tab_speechstart)
                return 1;
            else
                return 0;
        }

        void FormatAllText()
        {
            // přeformátuje holý text - hlavně zalamování řádků na pevnou šířku
            // momentálně se nepoužívá

            int i = 0;
            while (i < richTextBox_scene.Lines.Length)
            {
                string line = richTextBox_scene.Lines[i];
                // je na řádku vůbec něco?
                if (line.Length > 0)
                {
                    // nejdřív by to chtělo zjistit, o jaký typ řádku jde
                    int linetype = GetLineType(line);

                    //
                    if (linetype == 1)  //tab_speechend
                    {
                        if (line.Length > tab_speechend)
                        {
                            richTextBox_scene.SelectionStart = richTextBox_scene.GetFirstCharIndexFromLine(i) + tab_speechend;
                            richTextBox_scene.SelectedText = Environment.NewLine + space_tab_speechstart;
                        }
                    }
                    else if (linetype == 0)  //tab_end
                    {
                        if (line.Length > tab_end)
                        {
                            richTextBox_scene.SelectionStart = richTextBox_scene.GetFirstCharIndexFromLine(i) + tab_end;
                            richTextBox_scene.SelectedText = Environment.NewLine;
                        }
                    }
                }
                //
                i++;
            }
        }

        void ColorAllText()
        {
            // obarvuje text podle jmen a podobně
            //*** tady je nejspíš opět nějaký problém s číslováním řádků při zalamování

            // scanujeme postupně řádky
            int i = 0;
            Color currentcolor = Color.Black;
            int posstart = -1;
            int posend;

            // uložíme si pozici kurzoru
            int pos = richTextBox_scene.SelectionStart;

            while (i < richTextBox_scene.Lines.Length)
            {
                string line = richTextBox_scene.Lines[i];

                // zjistíme typ odstavce podle velikosti okraje
                int posline = richTextBox_scene.GetFirstCharIndexFromLine(i);
                richTextBox_scene.SelectionStart = posline;
                if (line.Length > 0 && richTextBox_scene.SelectionIndent >= margin_character)
                {
                    // vybereme barvu a uložíme si místo, kde bude začínat
                    // volba barvy podle jménoa
                    if (line[0] == 'K')
                        currentcolor = Color.Red;
                    else
                        currentcolor = Color.Blue;

                    // začátek selekce
                    posstart = posline;
                }

                // prázdný řádek znamená konec barvení
                if ((line.Length == 0 || i == richTextBox_scene.Lines.Length - 1) && posstart > 0)
                {
                    // konec selekce
                    // tohle je problém - pokud je řádek prázdný, tak první znak neexistuje!
                    // takže bereme začátek následujícího řádku
                    posend = richTextBox_scene.GetFirstCharIndexFromLine(i) - 1;
                    if (i == richTextBox_scene.Lines.Length - 1)
                    {
                        posend += line.Length;
                    }


                    richTextBox_scene.SelectionStart = posstart;
                    richTextBox_scene.SelectionLength = posend - posstart;
                    richTextBox_scene.SelectionColor = currentcolor;
                    richTextBox_scene.SelectionLength = 0;

                    posstart = -1;
                }

                i++;
            }

            // vrátíme kurzor na původní místo
            richTextBox_scene.SelectionStart = pos;
        }

        void UpdateCursorPosition()
        {
            // výpis pozice kurzoru
            int pos = richTextBox_scene.SelectionStart;
            int linenum = richTextBox_scene.GetLineFromCharIndex(richTextBox_scene.SelectionStart);
            int linestart = richTextBox_scene.GetFirstCharIndexOfCurrentLine();
            int linepos = pos - linestart;
            label_pos.Text = pos.ToString() + " / " + linenum.ToString() + " x " + linepos.ToString();
        }



        private void richTextBox_MouseUp(object sender, MouseEventArgs e)
        {
            UpdateCursorPosition();
        }

        private int GetScenePanelID(Panel panel)
        {
            int id = -1;
            string panelid = panel.Name.Substring(11);

            try
            {
                id = Int32.Parse(panelid);
            }
            catch (FormatException) { }

            return id;
        }

        private void ClickScenePanel(Panel panel)
        {
            // zjistíme si, na kterou scénu se kliklo
            int id = GetScenePanelID(panel);

            // číslování je posunuté o 1
            id--;

            // našli jsme něco
            if (id >= 0)
            {
                // deselect scény
                DeselectScene();
                // načteme scénu do pravého panelu
                SelectScene(id); // panel
            }
        }


        private void panel_scene_MouseDown(object sender, MouseEventArgs e)
        {
            // metoda tu musí zůstat, aby se inicializoval drag and drop

            // současně to zablokuje mouseclick a mouseup
            // výsledek je tedy zřejmě třeba řešit v rámci dragdrop end
            //base.OnMouseDown(e);
            DoDragDrop(sender, DragDropEffects.All);
        }

        private void panel_scene_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.Move;
        }

        private void ResizeAllScenePanels()
        {
            // resize panelů se scénami je třeba provést ručně
            flowLayoutPanel_scenes_list.SuspendLayout();

            foreach (Control ctrl in flowLayoutPanel_scenes_list.Controls)
            {
                // -6 mi vyšlo experimentálně - asi kvůli margins panelu
                // - 23 je poslední hodnota, kdy se aotmaticky neobjevuje horizontal bar
                ctrl.Width = flowLayoutPanel_scenes_list.ClientSize.Width - 6;
            }

            flowLayoutPanel_scenes_list.ResumeLayout();
        }


        private void flowLayoutPanel_scenelist_Resize(object sender, EventArgs e)
        {
            // pokud se změní rozměr panelu se scénami, tak stejně roztáhneme panely všech scén
            ResizeAllScenePanels();
        }

        private void DeselectScene()
        {
            // umažeme nadbytečné entery na konci okna (nesmí být dva po sobě)
            while (richTextBox_scene.Lines.Length > 2 && richTextBox_scene.Lines[richTextBox_scene.Lines.Length - 1] == "" && richTextBox_scene.Lines[richTextBox_scene.Lines.Length - 2] == "")
            {
                // vrátíme se o znak dozadu a smažeme ho
                richTextBox_scene.SelectionStart = richTextBox_scene.Text.Length - 1;
                richTextBox_scene.SelectionLength = 1;
                richTextBox_scene.SelectedText = "";
            }

            // uložíme si text z okna do scénáře
            currentscene.rtf = richTextBox_scene.Rtf;

            // vypneme zvýraznění panelu
            if (currentscenepanel != null)
            {
                currentscenepanel.BorderStyle = BorderStyle.None;
            }

            // kontrolní hlášení
            label_info.Text = "Scene deselected";
        }

        //private void SelectScene(int id, object sender = null)
        private void SelectScene(int id)
        {
            // kontrola proti pádu
            if (id >= screenplay.scenelist.Count)
            {
                return;
            }

            //Panel newselected = sender as Panel;
            Panel newselected = screenplay.scenelist[id].panelscene;

            // zvýraznění panelů
            // zapneme nový
            if (newselected != null)
            {
                newselected.BorderStyle = BorderStyle.FixedSingle;
            }

            // změníne aktivní položky - je to třeba dělat před přepsáním hodnot panelů
            currentscene = screenplay.scenelist[id];
            currentscenepanel = newselected;

            // tohle by mělo řešit falešné unsaved changes
            currentscenepanel.Focus();

            // překreslíme pravý panel
            label_scenenumber.Text = currentscene.order.ToString();
            panelHeader.BackColor = currentscene.color;
            textBox_scenename.Text = currentscene.name;
            textBox_heading.Text = currentscene.heading;
            richTextBox_scene.Rtf = currentscene.rtf;

            // text highlighting
            ColorAllText();

        }

        private void label_parentscene_MouseDown(object sender, MouseEventArgs e)
        {
            panel_scene_MouseDown((sender as Label).Parent, e);
        }

        private void UpdatePanelHeight(Scene scene)
        {
            int minheight = 20;
            if (checkBox_showname.Checked && checkBox_showheading.Checked)
                minheight += 20;

            if (comboBox_panelheight.SelectedIndex > 0)
            {
                int koef = 5;
                if (comboBox_panelheight.SelectedIndex == 1)
                    koef = 10;

                int newheight = scene.length / koef;
                if (newheight < minheight)
                    newheight = minheight;
                scene.panelscene.Height = newheight;
            }
            else
                scene.panelscene.Height = minheight;

            // pozice label_sceneheading se mění podle přítomnosti label_scenename
            if (checkBox_showname.Checked)
                scene.labelsceneheading.Location = new System.Drawing.Point(30, 23);
            else
                scene.labelsceneheading.Location = new System.Drawing.Point(30, 3);
        }

        private void UpdateAllPanelHeights()
        {
            // aktualizujeme výšku všech panelů podle nastavení checkboxů a délky scén

            // změna výšky panelů
            flowLayoutPanel_scenes_list.SuspendLayout();

            foreach (Scene scene in screenplay.scenelist)
            {
                UpdatePanelHeight(scene);
            }
            flowLayoutPanel_scenes_list.ResumeLayout();
        }

        private void checkBox_relativeheight_CheckedChanged(object sender, EventArgs e)
        {
            UpdateAllPanelHeights();
        }

        private void textBox_scenename_KeyDown(object sender, KeyEventArgs e)
        {
            // enter by měl přesunout kurzor o pole níže
            if (e.KeyCode == Keys.Enter)
            {
                textBox_heading.Focus();
            }
        }

        private void textBox_heading_KeyDown(object sender, KeyEventArgs e)
        {
            // enter by měl přesunout kurzor o pole níže
            if (e.KeyCode == Keys.Enter)
            {
                richTextBox_scene.Focus();
            }
        }

        private void tabControl_scenes_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Scenes
            if (tabControl_categories.SelectedIndex == 0)
            {
                // reload scény
                SelectScene(currentscene.order - 1);
            }
            // Export
            else if (tabControl_categories.SelectedIndex == 2)
            {
                textBox_export.Text = screenplay.GeneratePreview(richTextBox_scene);

                // aktualizujeme jména textboxů
                textBox_namescreenplay.Text = screenplay.docname;
                textBox_namefile.Text = screenplay.filename;
            }
        }

        private void splitContainer_Panel1_Resize(object sender, EventArgs e)
        {
            // potřebuju vnutit výšku levého panelu přes skoro celé okno
            flowLayoutPanel_scenes_list.Height = splitContainer_scenes.Panel1.ClientSize.Height - flowLayoutPanel_scenes_options.Height - 6;

            // flowLayoutPanel_scenes_main se korektně roztahuje sám
        }

        private void ResizeScenesFlowPanel()
        {
            // rámeček se seznamem scén by se měl roztáhnout podle rozměru celého levého svislého flowpanelu
            flowLayoutPanel_scenes_list.Width = flowLayoutPanel_scenes_main.Width;
            flowLayoutPanel_scenes_options.Width = flowLayoutPanel_scenes_main.Width;

            // hack na vypnutí horizontálního scrollbaru
            // zajímavé je, že jinde to nefunguje - asi se to něčím přepisuje
            flowLayoutPanel_scenes_list.HorizontalScroll.Maximum = 50;
        }

        private void textBox_scenename_TextChanged(object sender, EventArgs e)
        {
            // uložení dat z políčka do hlavního schromaždiště
            currentscene.name = textBox_scenename.Text;

            // přepsání položky v kartičce
            if (currentscene != null && currentscene.labelscenename != null)
            {
                currentscene.labelscenename.Text = textBox_scenename.Text;
            }

            // neuložená změna
            if (textBox_scenename.Focused)
                screenplay.unsaved = true;
        }

        private void textBox_heading_TextChanged(object sender, EventArgs e)
        {
            // uložení dat z políčka do hlavního schromaždiště
            currentscene.heading = textBox_heading.Text;

            // přepsání položky v kartičce
            //currentscenepanel.GetChildAtPoint(new Point(35, 5)).Text = textBox_scenename.Text;

            // neuložená změna
            if (textBox_heading.Focused)
                screenplay.unsaved = true;
        }

        private void panel_scene_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(typeof(Panel)) != null)
            {
                // panels to be swapped
                Panel r = (Panel)e.Data.GetData(typeof(Panel));
                Panel s = (sender as Panel);

                // posunuli jsme se na jiný objekt?
                if (r == s)
                {
                    // jsme stále na stejném objektu, takže je to v podstatě normální kliknutí
                    // label_info.Text = "Normal click";
                    ClickScenePanel(sender as Panel);
                }
                else
                {
                    // jsme na jiném objektu

                    // p = background flowpanel
                    FlowLayoutPanel p = (FlowLayoutPanel)(sender as Panel).Parent;

                    // positions of panels in flowpanel
                    // int indexr = p.Controls.GetChildIndex(r);
                    int indexs = p.Controls.GetChildIndex(s);

                    p.Controls.SetChildIndex(r, indexs);

                    // update order values in screenplay object
                    foreach (Scene scene in screenplay.scenelist)
                    {
                        scene.order = p.Controls.GetChildIndex(scene.panelscene) + 1;
                        scene.labelorder.Text = scene.order.ToString();
                    }

                    // sort the scenes in screenplay object
                    screenplay.scenelist.Sort();

                    // přečíslujeme scény
                    UpdateOrdering();

                    // update number of scene in right panel
                    label_scenenumber.Text = currentscene.order.ToString();

                }
            }

        }

        private void SaveExport(string filename)
        {
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(filename))
            {
                file.WriteLine(textBox_export.Text);
            }

        }

        private void button_ExportToFile_Click(object sender, EventArgs e)
        {
            SaveExport(screenplay.filename + ".txt");
        }

        private void pd_PrintPage(object sender, PrintPageEventArgs ev)
        {
            float linesPerPage;
            float yPos;
            int count = 0;
            float leftMargin = ev.MarginBounds.Left;
            float topMargin = ev.MarginBounds.Top;
            String line = null;

            // Calculate the number of lines per page.
            linesPerPage = ev.MarginBounds.Height /
               printFont.GetHeight(ev.Graphics);

            // Iterate over the file, printing each line.
            while (count < linesPerPage &&
               ((line = streamToPrint.ReadLine()) != null))
            {
                yPos = topMargin + (count * printFont.GetHeight(ev.Graphics));
                ev.Graphics.DrawString(line, printFont, Brushes.Black,
                   leftMargin, yPos, new StringFormat());
                count++;
            }

            // If more lines exist, print another page.
            if (line != null)
                ev.HasMorePages = true;
            else
                ev.HasMorePages = false;
        }

        // Print the file.
        public void Printing()
        {
            // tohle taky tiskne ze souboru a taky se to na nic neptá...!
            // ale minimálně to neotevírá notepad =)

            string filePath = "export.txt";
            try
            {
                streamToPrint = new StreamReader(filePath);
                try
                {
                    printFont = new Font("Courier New", 12);
                    PrintDocument pd = new PrintDocument();
                    pd.PrintPage += new PrintPageEventHandler(pd_PrintPage);
                    // Print the document.
                    pd.Print();
                }
                finally
                {
                    streamToPrint.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }


        private void button_print_Click(object sender, EventArgs e)
        {
            //PrintText(textBox_export.Text);
            Printing();
        }

        private void textBox_namescreenplay_TextChanged(object sender, EventArgs e)
        {
            screenplay.docname = textBox_namescreenplay.Text;
        }

        private void textBox_namefile_TextChanged(object sender, EventArgs e)
        {
            screenplay.filename = textBox_namefile.Text;
        }

        private void label_addscene_Click(object sender, EventArgs e)
        {
            AddNewScene();
        }

        private void richTextBox_scene_TextChanged(object sender, EventArgs e)
        {
            // aktualizujeme délku scény ve scénáři i v panelu
            currentscene.length = richTextBox_scene.Text.Length;
            currentscene.labelscenelength.Text = richTextBox_scene.Text.Length.ToString();

            // aktualizujeme velikost panelu "kartičky"
            if (comboBox_panelheight.SelectedIndex > 0)
                UpdatePanelHeight(currentscene);

            // pokud píšeme jméno postavy, tak poslední znak změníme na uppercase
            if (richTextBox_scene.SelectionIndent == margin_character)
            {
                if (richTextBox_scene.SelectionStart > 0)
                    richTextBox_scene.SelectionStart--;
                richTextBox_scene.SelectionLength = 1;
                richTextBox_scene.SelectedText = richTextBox_scene.SelectedText.ToUpper();
                // pozice a selekece se automatricky vyresetují
            }

            // neuložená změna
            if (richTextBox_scene.Focused)
                screenplay.unsaved = true;
        }

        private void richTextBox_scene_Leave(object sender, EventArgs e)
        {
            DeselectScene();
        }

        private void button_deletescene_Click(object sender, EventArgs e)
        {
            DeleteScene(currentscene);
        }

        private void UpdateVisibilityAllNames()
        {
            foreach (Scene scene in screenplay.scenelist)
            {
                if (checkBox_showname.Checked)
                    scene.labelscenename.Show();
                else
                    scene.labelscenename.Hide();
            }
        }

        private void checkBox_showname_CheckedChanged(object sender, EventArgs e)
        {
            UpdateVisibilityAllNames();
            UpdateAllPanelHeights();

        }

        private void UpdateVisibilityAllHeadings()
        {
            foreach (Scene scene in screenplay.scenelist)
            {
                if (checkBox_showheading.Checked)
                    scene.labelsceneheading.Show();
                else
                    scene.labelsceneheading.Hide();
            }
        }

        private void checkBox_showheading_CheckedChanged(object sender, EventArgs e)
        {
            UpdateVisibilityAllHeadings();
            UpdateAllPanelHeights();
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // check for unsaved changes
            if (screenplay.unsaved == true)
            {
                var res = MessageBox.Show(localization.UnsavedChangesQuestion, localization.UnsavedChangesCaption, MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);

                // If the no button was pressed ...
                if (res == DialogResult.No)
                    // cancel the closure of the main form.
                    return;
            }

            openFileDialog.InitialDirectory = path_initial;
            //openFileDialog.CheckFileExists = true;
            DialogResult result = openFileDialog.ShowDialog(this);
            if (result == DialogResult.OK) // Test result.
            {
                DeleteAllScenes();
                //LoadScreenplay("pokus" + extension);
                LoadScreenplay(openFileDialog.FileName);
                SelectScene(0);
            }
        }

        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // check for unsaved changes
            if (screenplay.unsaved == true)
            {
                var result = MessageBox.Show(localization.UnsavedChangesQuestion, localization.UnsavedChangesCaption, MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation);

                // If the no button was pressed ...
                if (result == DialogResult.No)
                    // cancel the closure of the main form.
                    return;
            }

            DeleteAllScenes();

            AddNewScene();
            SelectScene(0);
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // uložení do souboru
            // je třeba zajistit, že se text z richtextboxu přenese do scénáře
            DeselectScene();
            //SelectScene(currentscene.order-1);
            SaveScreenplay(screenplay.filename + extension);
        }

        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            saveFileDialog.InitialDirectory = path_initial;
            DialogResult result = saveFileDialog.ShowDialog(this);
            if (result == DialogResult.OK) // Test result.
            {
                // zpracujeme jméno souboru
                string filename = saveFileDialog.FileName;
                filename = filename.Substring(0, filename.IndexOf(extension));
                screenplay.filename = filename;


                // uložení do souboru
                // je třeba zajistit, že se text z richtextboxu přenese do scénáře
                DeselectScene();
                //SelectScene(currentscene.order-1);
                SaveScreenplay(screenplay.filename + extension);
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void flowLayoutPanel_scenes_main_Resize(object sender, EventArgs e)
        {
            ResizeScenesFlowPanel();
        }

        private void FormMain_Resize(object sender, EventArgs e)
        {
            if (conf.splitterdistance >= 150)   // minimální použitelná šířka
            {
                // sem bychom se měli dostat pouze v případě, že je při startu zapnuté maximized
                //*** není to úplně stoprocentní řešení

                // zdá se, že SplitterDistance pracuje s pozicí v rámci Normal okna
                // Maximize to celé nějak rozhodí

                // první problém je zřejmě MinimumSize vnořených panelů
                splitContainer_scenes.Panel1MinSize = splitContainer_scenes.Panel1MinSize * 935 / Width;

                // hlavní problém se nutnost přepočítat splitterdistance
                splitContainer_scenes.SplitterDistance = conf.splitterdistance * 935 / Width;
                conf.splitterdistance = 0;
            }
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FormAbout formabout = new FormAbout();
            formabout.StartPosition = FormStartPosition.Manual;
            formabout.Location = new Point(Location.X + Width / 3, Location.Y + Height / 4);
            formabout.label_version.Text = "version " + version.Major + "." + version.Minor;
            formabout.ShowDialog();
        }

        private void comboBox_panelheight_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateAllPanelHeights();
        }
    }

    public class Configuration
    {
        public FormWindowState windowstate;
        public Point location;
        public Size size;
        public int splitterdistance;
        public bool displayname;
        public bool displayheading;
        public int heighttype;
        public string latestfilename;
        public bool reopenlatestfile;

        Configuration()
        {
            windowstate = FormWindowState.Normal;
            splitterdistance = -1;
            latestfilename = "";
            reopenlatestfile = false;
            displayname = true;
            displayheading = true;
            heighttype = 0;
        }

        public void SaveXMLConfig()
        {
            try
            {
                var writer = new System.Xml.Serialization.XmlSerializer(typeof(Configuration));
                var wfile = new System.IO.StreamWriter("configuration.xml");
                writer.Serialize(wfile, this);
                wfile.Close();
            }
            catch
            {
            }
        }


        public static Configuration LoadXMLConfig()
        {
            Configuration result = new Configuration();
            try
            {
                System.Xml.Serialization.XmlSerializer reader = new System.Xml.Serialization.XmlSerializer(typeof(Configuration));
                System.IO.StreamReader file = new System.IO.StreamReader("configuration.xml");
                result = (Configuration)reader.Deserialize(file);
                file.Close();
            }
            catch
            {
            }
            return result;
        }
    }

    public class Scene : IComparable<Scene>
    {
        //[XmlElement(ElementName = "Order")]
        public int order;       // pořadí ve výsledku

        [XmlIgnore()]           // následující položka se pak nezahrne do xml
        public Color color;     // rozlišovací barva

        [XmlElement("Color")]
        public int BackColorAsArgb
        {
            get { return color.ToArgb(); }
            set { color = Color.FromArgb(value); }
        }

        public string name;     // orientační jméno scény
        public string heading;  // aka slugline
        public int length;      // délka scény ve znacích
        public string rtf;      // text

        // pracovní zkratky na objekty, které scénu zastupují
        [XmlIgnore()]
        public Panel panelscene;
        [XmlIgnore()]
        public Label labelorder;
        [XmlIgnore()]
        public Label labelscenename;
        [XmlIgnore()]
        public Label labelsceneheading;
        [XmlIgnore()]
        public Label labelscenelength;

        public Scene()
        {
            order = 0;
            color = Color.WhiteSmoke;
            name = "Scene";
            heading = "INT. LOCATION - DAYTIME";
            rtf = "";
            //
            panelscene = null;
            labelscenename = null;
            labelsceneheading = null;
        }

        public int CompareTo(Scene comparePart)
        {
            // A null value means that this object is greater.
            if (comparePart == null)
                return 1;

            else
                return this.order.CompareTo(comparePart.order);
        }

    }

    public class Screenplay
    {

        //public const int scenemax = 100;
        public string docname;      // jméno scénáře
        public string filename;     // krátké jméno souboru bez přípony
        public bool unsaved;        // příznak, že existují neuložené změny

        [XmlArray(ElementName = "SceneList")]
        public List<Scene> scenelist = new List<Scene>();

        public Screenplay()
        {
            unsaved = false;
        }

        public string GeneratePreview(RichTextBox rtb)
        {
            string result = "";

            // titulní strana
            for (int k = 0; k < 10; k++)
                result += Environment.NewLine;
            result += Environment.NewLine + FormMain.space_tab_character + docname + Environment.NewLine + Environment.NewLine;
            for (int k = 0; k < 10; k++)
                result += Environment.NewLine;
            result += FormMain.space_tab_character + "Vít Čondák" + Environment.NewLine;
            for (int k = 0; k < 10; k++)
                result += Environment.NewLine;


            for (int i = 0; i < scenelist.Count; i++)
            {
                // heading
                result += scenelist[i].order + "    " + scenelist[i].heading + Environment.NewLine + Environment.NewLine;

                // screenplay
                // pro konverzi použijeme existující richTextBox - je to lepší i kvůli konzistenci
                rtb.Rtf = scenelist[i].rtf;
                string plaintext = ""; // rtb.Text;

                int j = 0;
                int pos = rtb.GetFirstCharIndexFromLine(j);
                while (pos >= 0)
                {
                    rtb.SelectionStart = pos;

                    int posnext = rtb.GetFirstCharIndexFromLine(j + 1);
                    if (posnext > pos)
                        // následující řádek
                        rtb.SelectionLength = posnext - pos - 1; //*** tohle by mělo zajistit useknutí CRLF, ale je to trochu provizorka
                    else
                    {
                        // konec souboru
                        //*** dost pofiderní hack, ale chybu to neháže
                        rtb.SelectionLength = 100;
                    }

                    // zvětšení okraje pomocí mezer
                    // postava
                    if (rtb.SelectionIndent >= FormMain.margin_character)
                        plaintext += FormMain.space_tab_character;
                    // řeč
                    else if (rtb.SelectionIndent >= FormMain.margin_speechstart)
                        plaintext += FormMain.space_tab_speechstart;

                    // hlavní text
                    //*** někde tady může docházet ke zdvojování CR LF - nebo spíš jenom jednoho z nich
                    // asi by bylo jistější je preventivně očesávat
                    plaintext += "     " + rtb.SelectedText + Environment.NewLine;

                    // zjistíme pozici dalšího řádku
                    j++;
                    pos = posnext;
                }


                result += plaintext + Environment.NewLine;

            }
            return result;
        }
    }
}
