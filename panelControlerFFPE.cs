using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;
using OmegaScript.Properties;

namespace OmegaScript.Scripts
{
    public partial class panelControlerFFPE : Form //UserControl
    {
        
        public panelControlerFFPE()
        {
            InitializeComponent();
            //speedDisrupBar.ValueChanged += speedDisrupBar_ValueChanged;

            #region Disabled the five sections
            caliBtn.Enabled = false;
            runBtn.Enabled = false;
            //refreshBtn.Enabled = false;

            //Mixing
            topMixBtn.Enabled = false;
            immerBtn.Enabled = false;
            spdMixBar.Enabled = false;
            timeMixUpDown.Enabled = false;
            mixTriturateBtn.Enabled = false;

            //Disruption Section
            disruptTabControl.Enabled = false;

            //Delivery Section
            halfMlBtn.Enabled = false;
            oneMlBtn.Enabled = false;
            twoMlBtn.Enabled = false;
            spdDelBar.Enabled = false;
            leftSSCheckBox.Enabled = false;
            rightSSCheckBox.Enabled = false;
            fluUpDown.Enabled = false;
            customFluBtn.Enabled = false;

            //Strain
            halfStrainBtn.Enabled = false;
            fullStrainBtn.Enabled = false;
            doubleStrainBtn.Enabled = false;
            spdStrainBar.Enabled = false;
            #endregion
        }

        #region Instances & Variables 
        OmegaScript protocol;
        OmegaScript.Instrument myInstrument;
        strainHalf halfQuestion = new strainHalf();
        disruptProfileKey theKey = new disruptProfileKey();
        
        //public ProtocolParams pParams;
        

        public string myDialogResult;
        public int myTest, myCase;
        //public int myCase;
        public int setMixSpd = 145;
        public int setDisrupSpd = 95;
        public int setDeliverySpd = 200;
        public int setStrainSpd = 200;
        public int valvePos;
        public int solutionDeliver;
        public int airPrime;
        public int strainSteps;
        public int whichCatch;
        public int incubationTime = 1;
        public int runReady;
        public int setTempBlock = 57;
        public double desiredAmt;
        public bool myAuto = false;
        public bool selectedSS = false;
        public bool refreshBool = false;
        public int[] customZProfile, customRoboProfile;
        public string tempType;
        #endregion


        #region Temp Control

        private void tempInput_ValueChanged(object sender, EventArgs e)
        {
            if (tempInput.Value > 20)
            {
                tempInput.ForeColor = Color.Red; 
            }
            else if (tempInput.Value < 1)
            {
                tempInput.ForeColor = Color.DeepSkyBlue;
            }
            else
            {
                tempInput.ForeColor = Color.White;
            }

            setTempBlock = Convert.ToInt16(tempInput.Value);
        }

        private void ffpeTempBtn_CheckedChanged(object sender, EventArgs e)
        {
            myCase = 3;
            
            if(ffpeTempBtn.Checked == true)
            {
                tempType = "ffpe";
                //this.Hide();
            }
        }

        private void cellTempBtn_CheckedChanged(object sender, EventArgs e)
        {
            myCase = 3;
            
            if(rampDownTempBtn.Checked == true)
            {
                tempType = "rampDown";
                //this.Hide();
            }
        }

        private void nucTempBtn_CheckedChanged(object sender, EventArgs e)
        {
            myCase = 3;
            
            if(steadyTempBtn.Checked == true)
            {
                tempType = "steady";
                //this.Hide();
            }
        }


        private void tempBtn_Click(object sender, EventArgs e)
        {
            myCase = 3;
            myTest = 0;
            this.Hide();

        }

        private void tempOffBtn_Click(object sender, EventArgs e)
        {
            myCase = 12;
            myTest = 1;
            this.Hide();
        }
        #endregion

        #region Mixing 
        private void topMixBtn_Click(object sender, EventArgs e)
        {
            myDialogResult = "top mix";
            myCase = 7;
            myTest = 10;
            runReady++;

            halfQuestion.ShowDialog();
            whichCatch = halfQuestion.whichOne;

            if (runReady == 1)
            {
                runBtn.Enabled = true;
                runBtn.BackColor = Color.LightGreen;
                immerBtn.Enabled = false;
                mixTriturateBtn.Enabled = false;
            }
            
            //if (runReady == 1 && runBtn.Enabled)
            //{
            //    runBtn.Enabled = false;
            //    immerBtn.Enabled = true;
            //    runReady--;
            //}
            //protocol.ScriptLog(OmegaScript.Severity.Control, $"RunReady= {runReady}");
            //this.Hide();
            //protocol.BumpScript(7);
        }

        private void immerBtn_Click(object sender, EventArgs e)
        {
            myDialogResult = "immersion mix";
            myCase = 7;
            myTest = 11;

            halfQuestion.ShowDialog();
            whichCatch = halfQuestion.whichOne;

            runReady++;
            if (runReady == 1)
            {
                runBtn.Enabled = true;
                runBtn.BackColor = Color.LightGreen;
                topMixBtn.Enabled = false;
                mixTriturateBtn.Enabled = false;
            }
            //this.Hide();
            //protocol.BumpScript(7);
        }

        private void mixTriturateBtn_Click(object sender, EventArgs e)
        {
            myCase = 7;
            myTest = 14;

            halfQuestion.ShowDialog();
            whichCatch = halfQuestion.whichOne;

            runReady++;
            if (runReady == 1)
            {
                runBtn.Enabled = true;
                runBtn.BackColor = Color.LightGreen;
                topMixBtn.Enabled = false;
                immerBtn.Enabled = false;
            }

        }

        public void spdMixBar_Scroll(object sender, EventArgs e)
        {
            //create conditional to set the rpm depending on the behavior.value
            myDialogResult = "mix speed";
            myTest = 12;
            // mixingSpeed = 45 + (pParams.ControlParameters.MixingSpeed * 25);
            //protocolPara.ControlPara.mixingSpeed = 4;

            if (spdMixBar.Value == 0) //slowest
            {
                //setMixSpd = 45 + (2 * 25);
                setMixSpd = 70;
            }
            else if (spdMixBar.Value == 1) //slow
            {
                //setMixSpd = 45 + (3 * 25);
                setMixSpd = 95;
            }
            else if (spdMixBar.Value == 2) //medium
            {
                //setMixSpd = 45 + (4 * 25);
                setMixSpd = 120;
            }
            else if (spdMixBar.Value == 3) //fast
            {
                //setMixSpd = 45 + (5 * 25);
                setMixSpd = 145;
            }
            else //fastest
            {
                //setMixSpd = 45 + (6 * 25);
                setMixSpd = 170;
            }
            
        }

        private void timeMixUpDown_ValueChanged(object sender, EventArgs e)
        {
            myDialogResult = "incubation time";
            myTest = 13;
            //runReady++;
            incubationTime = Convert.ToInt16(timeMixUpDown.Value);
            
        }
        #endregion

        #region Disruption

        #region ShortCuts
        private void defaultDisBtn_Click(object sender, EventArgs e)
        {
            myDialogResult = "default disrup";
            myCase = 8;
            myTest = 20;
            runReady++;
            if (runReady <= 1)
            {
                runBtn.Enabled = true;
                runBtn.BackColor = Color.LightGreen;
                triturateBtn.Enabled = false;
                lungDisBtn.Enabled = false;
                nucDisBtn.Enabled = false;
                dounceBtn.Enabled = false;
            }
            //this.Hide();
            //protocol.BumpScript(8);
        }

        private void lungDisBtn_Click(object sender, EventArgs e)
        {
            myCase = 8;
            myTest = 23;
            runReady++;
            if (runReady <= 1)
            {
                runBtn.Enabled = true;
                runBtn.BackColor = Color.LightGreen;
                triturateBtn.Enabled = false;
                defaultDisBtn.Enabled = false;
                nucDisBtn.Enabled = false;
                dounceBtn.Enabled = false;
            }
        }

        private void triturateBtn_Click(object sender, EventArgs e)
        {
            myDialogResult = "triturate disrup";
            myCase = 8;
            myTest = 21;
            runReady++;
            if (runReady <= 1)
            {
                runBtn.Enabled = true;
                runBtn.BackColor = Color.LightGreen;
                defaultDisBtn.Enabled = false;
                lungDisBtn.Enabled = false;
                nucDisBtn.Enabled = false;
                dounceBtn.Enabled = false;
            }

            //this.Hide();
            //protocol.BumpScript(8);
        }

        private void nucDisBtn_Click(object sender, EventArgs e)
        {
            myCase = 8;
            myTest = 24;
            runReady++;
            if (runReady <= 1)
            {
                runBtn.Enabled = true;
                runBtn.BackColor = Color.LightGreen;
                triturateBtn.Enabled = false;
                defaultDisBtn.Enabled = false;
                lungDisBtn.Enabled = false;
                dounceBtn.Enabled = false;
            }
        }

        private void dounceBtn_Click(object sender, EventArgs e)
        {
            myCase = 8;
            myTest = 25;
            runReady++;

            halfQuestion.ShowDialog();
            whichCatch = halfQuestion.whichOne;

            if (runReady <= 1)
            {
                runBtn.Enabled = true;
                runBtn.BackColor = Color.LightGreen;
                triturateBtn.Enabled = false;
                defaultDisBtn.Enabled = false;
                lungDisBtn.Enabled = false;
                nucDisBtn.Enabled = false;
            }
        }

        private void speedDisrupBar_Scroll(object sender, EventArgs e)
        {

            myDialogResult = "Disrup Speed";
            myTest = 22;

            if (speedDisrupBar.Value == 0) //slowest
            {
                setDisrupSpd = 45;
            }
            else if (speedDisrupBar.Value == 1) //slow
            {
                setDisrupSpd = 70;
            }
            else if (speedDisrupBar.Value == 2) //medium
            {
                setDisrupSpd = 95;
            }
            else if (speedDisrupBar.Value == 3) //fast
            {
                setDisrupSpd = 120;
            }
            else //fastest
            {
                setDisrupSpd = 145;
            }
            
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (autoMinceChkbox.Checked == true)
            {
                myDialogResult = "auto mince";
                myCase = 8;
                myAuto = true;

                runReady = 1;
                
            } 
            else
            {
                myAuto = false;
                runReady = 0;
            }
            if (runReady == 1)
            {
                runBtn.Enabled = true;
                runBtn.BackColor = Color.LightGreen;
            }
            else
            {
                runBtn.Enabled = false;
                runBtn.BackColor = Color.FloralWhite;
            }
        }
        #endregion

        #region Custom
        private void disruptInput_TextChanged(object sender, EventArgs e)
        {
            //disruptShowInput myZProfile = new disruptShowInput();

        }

        private void disruptInput_KeyPress(object sender, KeyPressEventArgs e)
        {

            if (Char.IsDigit(e.KeyChar) || (e.KeyChar == ',') || (e.KeyChar == (char)Keys.Back)) // || (e.KeyChar == (char)Keys.Space))
            {
                return;
            }

            e.Handled = true;
        }

        private void applyProfileBtn_Click(object sender, EventArgs e)
        {
            //int disInputInt = int.Parse(disruptInput.Text.ToString());
            string disInputString = disruptInput.Text.ToString();
            if (stepperRadioBtn.Checked == true)
            {
                
                customZProfile = disInputString.Split(',').Select(int.Parse).ToArray();

                showVertProfile.Text = disInputString;
            }

            if (roboRadioBtn.Checked == true)
            {
                customRoboProfile = disInputString.Split(',').Select(int.Parse).ToArray();

                showRoboProfile.Text = disInputString;
            }

            if (roboSpdRadioBtn.Checked == true)
            {
                setDisrupSpd = Convert.ToInt32(disruptInput.Text);

                showRotoSpd.Text = setDisrupSpd.ToString();
            }

            //customZProfile = Convert.ToInt16(disruptInput.Text).ToArray();


            //MessageBox.Show(disruptInput.Text, "Z Profile", MessageBoxButtons.OK);
            //string.Join(Environment.NewLine,customZProfile),
            //MaskedTextBox
        }

        private void disrupCusDelBtn_Click(object sender, EventArgs e)
        {
            disruptInput.Text = " ";
        }

        private void keyCusDisBtn_Click(object sender, EventArgs e)
        {
            theKey.ShowDialog();
        }

        private void checkCusDisBtn_Click(object sender, EventArgs e)
        {
            disruptTabControl.SelectTab(2);
        }

        private void customizeDisBtn_Click(object sender, EventArgs e)
        {
            disruptTabControl.SelectTab(1);
        }

        private void testCusDisBtn_Click(object sender, EventArgs e)
        {
            myCase = 8;
            myTest = 26;

            if (customZProfile.Length == customRoboProfile.Length)
            {
                runBtn.Enabled = true;
                runBtn.BackColor = Color.LightGreen;
            }

        }


        #endregion

        #endregion

        #region Delivery
        private void halfMlBtn_Click(object sender, EventArgs e)
        {
            myDialogResult = "half deliver";
            myCase = 9;
            myTest = 30;

            solutionDeliver = 300;
            airPrime = 600;

            runReady++;
            if (runReady == 2)
            {
                runBtn.Enabled = true;
                runBtn.BackColor = Color.LightGreen;
                oneMlBtn.Enabled = false;
                twoMlBtn.Enabled = false;
                fluUpDown.Enabled = false;
                customFluBtn.Enabled = false;
            }
            //this.Hide();
            //protocol.BumpScript(9);
        }

        private void oneMlBtn_Click(object sender, EventArgs e)
        {
            myDialogResult = "one deliver";
            myCase = 9;
            myTest = 31;

            solutionDeliver = 600;
            airPrime = 1200;

            runReady++;
            if (runReady == 2)
            {
                runBtn.Enabled = true;
                runBtn.BackColor = Color.LightGreen;
                halfMlBtn.Enabled = false;
                twoMlBtn.Enabled = false;
                fluUpDown.Enabled = false;
                customFluBtn.Enabled = false;
            }

            //this.Hide();
            //protocol.BumpScript(9);
        }

        private void twoMlBtn_Click(object sender, EventArgs e)
        {
            myDialogResult = "two deliver";
            myCase = 9;
            myTest = 32;

            solutionDeliver = 1200;
            airPrime = 1800;

            runReady++;
            if (runReady == 2)
            {
                runBtn.Enabled = true;
                runBtn.BackColor = Color.LightGreen;
                oneMlBtn.Enabled = false;
                halfMlBtn.Enabled = false;
                fluUpDown.Enabled = false;
                customFluBtn.Enabled = false;
            }
            //this.Hide();
            //protocol.BumpScript(9);
        }

        private void fluUpDown_ValueChanged(object sender, EventArgs e)
        {
            //     0<input<0.5
            // can take up to 1 decimal place
            // increments by 0.1

            myCase = 9;
            
            desiredAmt = Convert.ToDouble(fluUpDown.Value);

            
        }

        private void customFluBtn_Click(object sender, EventArgs e)
        {
            myCase = 9;
            myTest = 33;

            solutionDeliver = Convert.ToInt32(Math.Round(desiredAmt * 600.0));
            airPrime = 0;

            runReady++;
            if (desiredAmt < 0.3)
            {
                spdDelBar.Value = 0;
                setDeliverySpd = 100;
            }
            else
            {
                spdDelBar.Value = 1;
                setDeliverySpd = 200;
            }
            if (runReady == 2)
            {
                runBtn.Enabled = true;
                runBtn.BackColor = Color.LightGreen;
                oneMlBtn.Enabled = false;
                halfMlBtn.Enabled = false;
                halfMlBtn.Enabled = false;
            }
        }
        private void spdDelBar_Scroll(object sender, EventArgs e)
        {
            myDialogResult = "delivery speed";
            //AdjustPumpSpeed(200); ~ default/medium
            myTest = 33;
            
            if (spdDelBar.Value == 0) //slow
            {
                setDeliverySpd = 100;
            }
            else if (spdDelBar.Value == 1) //medium
            {
                setDeliverySpd = 200;
            }
            else //fast
            {
                setDeliverySpd = 300;
            }
        }

        private void leftSSCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (leftSSCheckBox.Checked == true)
            {
                myDialogResult = "SS left";
                
                valvePos = 10;
                selectedSS = true;
                rightSSCheckBox.Enabled = false;
                runReady++;
            }
            else
            {
                selectedSS = false;
                rightSSCheckBox.Enabled = true;
                runReady = 0;
                runBtn.Enabled = false;
                runBtn.BackColor = Color.FloralWhite;
                halfMlBtn.Enabled = true;
                oneMlBtn.Enabled = true;
                twoMlBtn.Enabled = true;
                fluUpDown.Enabled = true;
                customFluBtn.Enabled = true;
            }

            if (runReady == 2)
            {
                runBtn.Enabled = true;
                runBtn.BackColor = Color.LightGreen;
            }
        }

        private void rightSSCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (rightSSCheckBox.Checked == true)
            {
                myDialogResult = "SS right";
                
                valvePos = 11;
                selectedSS = true;
                leftSSCheckBox.Enabled = false;
                runReady++;
            }
            else
            {
                selectedSS = false;
                leftSSCheckBox.Enabled = true;
                runReady = 0;

                runBtn.Enabled = false;
                runBtn.BackColor = Color.FloralWhite;
                halfMlBtn.Enabled = true;
                oneMlBtn.Enabled = true;
                twoMlBtn.Enabled = true;
                fluUpDown.Enabled = true;
                customFluBtn.Enabled = true;
            }

            if (runReady == 2)
            {
                runBtn.Enabled = true;
                runBtn.BackColor = Color.LightGreen;
            }

        }

        #endregion

        #region Strain
        private void halfStrainBtn_Click(object sender, EventArgs e)
        {
            myDialogResult = "Half Strain";
            myCase = 10;
            myTest = 40;
            strainSteps = 1500;

            //halfQuestion = new strainHalf();
            halfQuestion.ShowDialog();
            whichCatch = halfQuestion.whichOne;

            runReady++;
            if (runReady == 1)
            {
                runBtn.Enabled = true;
                runBtn.BackColor = Color.LightGreen;
                fullStrainBtn.Enabled = false;
                doubleStrainBtn.Enabled = false;
            }

            //this.Hide();
            //protocol.BumpScript(10);
        }

        private void fullStrainBtn_Click(object sender, EventArgs e)
        {
            myDialogResult = "Full Strain";
            myCase = 10;
            myTest = 41;
            strainSteps = 3000;

            runReady++;
            if (runReady == 1)
            {
                runBtn.Enabled = true;
                runBtn.BackColor = Color.LightGreen;
                halfStrainBtn.Enabled = false;
                doubleStrainBtn.Enabled = false;
            }
            //this.Hide();
            //protocol.BumpScript(10);
        }

        private void doubleStrainBtn_Click(object sender, EventArgs e)
        {
            myDialogResult = "double strain";
            myCase = 10;
            myTest = 42;
            strainSteps = 3000;

            runReady++;
            if (runReady == 1)
            {
                runBtn.Enabled = true;
                runBtn.BackColor = Color.LightGreen;
                halfStrainBtn.Enabled = false;
                fullStrainBtn.Enabled = false;
            }
            //protocol.BumpScript(10);
        }

        private void spdStrainBar_Scroll(object sender, EventArgs e)
        {
            myDialogResult = "strain speed";
            myTest = 43;
            //200 --> after strain, the pumpSpd is reset to 600

            if (spdStrainBar.Value == 0) //slow
            {
                setStrainSpd = 100;
            }
            else if (spdStrainBar.Value == 1) //medium
            {
                setStrainSpd = 200;
            }
            else //fast
            {
                setStrainSpd = 300;
            }
        }
        #endregion

        #region Calibration
        private void caliCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            //protocol.ScriptLog(OmegaScript.Severity.Control, "bumping to MS: 6");
            ////protocol.BumpScriptEvent += protocol.BumpScriptEventHandler;

            myDialogResult = "calibration";
            myCase = 6;
            ////protocol.MachineState = 6;
            ////protocol.BumpScript(6);
            //protocol.ScriptLog(OmegaScript.Severity.Control, "Got bumped to MS: 6. myDialogResult: " + myDialogResult);
            //MessageBox.Show("You are in the CheckBox.CheckedChanged event.");
            this.Hide();
        }
        private void caliBtn_Click(object sender, EventArgs e)
        {
            myDialogResult = "calibration";
            myCase = 6;
            this.Hide();
        }


        #endregion

        #region PickNextStep
        private void nextStepComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            //refreshBool = true;

            //refreshBtn.Checked = false;
            runReady = 0;
            runBtn.Enabled = false;
            runBtn.BackColor = Color.FloralWhite;
            nextStepComboBox.ForeColor = Color.Black;

            if (nextStepComboBox.Text == "Mixing")
            {
                #region Mixing Enabled
                //Maintenance
                caliBtn.Enabled = false;


                //Mixing
                topMixBtn.Enabled = true;
                immerBtn.Enabled = true;
                mixTriturateBtn.Enabled = true;
                spdMixBar.Enabled = true;
                timeMixUpDown.Enabled = true;

                //Disruption Section
                disruptTabControl.Enabled = false;

                //Delivery Section
                halfMlBtn.Enabled = false;
                oneMlBtn.Enabled = false;
                twoMlBtn.Enabled = false;
                spdDelBar.Enabled = false;
                leftSSCheckBox.Enabled = false;
                rightSSCheckBox.Enabled = false;

                //Strain
                halfStrainBtn.Enabled = false;
                fullStrainBtn.Enabled = false;
                doubleStrainBtn.Enabled = false;
                spdStrainBar.Enabled = false;

                //refreshBool = false;
                #endregion
            }

            else if (nextStepComboBox.Text == "Disruption")
            {
                #region Disruption Enabled
                caliBtn.Enabled = false;

                //Mixing
                topMixBtn.Enabled = false;
                immerBtn.Enabled = false;
                spdMixBar.Enabled = false;
                mixTriturateBtn.Enabled = false;
                timeMixUpDown.Enabled = false;

                //Disruption Section
                disruptTabControl.Enabled = true;

                //Delivery Section
                halfMlBtn.Enabled = false;
                oneMlBtn.Enabled = false;
                twoMlBtn.Enabled = false;
                spdDelBar.Enabled = false;
                leftSSCheckBox.Enabled = false;
                rightSSCheckBox.Enabled = false;

                //Strain
                halfStrainBtn.Enabled = false;
                fullStrainBtn.Enabled = false;
                doubleStrainBtn.Enabled = false;
                spdStrainBar.Enabled = false;
                #endregion
            }
            else if (nextStepComboBox.Text == "Delivery")
            {
                #region Delivery Enabled
                caliBtn.Enabled = false;

                //Mixing
                topMixBtn.Enabled = false;
                immerBtn.Enabled = false;
                spdMixBar.Enabled = false;
                mixTriturateBtn.Enabled = false;
                timeMixUpDown.Enabled = false;

                //Disruption Section
                disruptTabControl.Enabled = false;

                //Delivery Section
                halfMlBtn.Enabled = true;
                oneMlBtn.Enabled = true;
                twoMlBtn.Enabled = true;
                spdDelBar.Enabled = true;
                leftSSCheckBox.Enabled = true;
                rightSSCheckBox.Enabled = true;
                fluUpDown.Enabled = true;
                customFluBtn.Enabled = true;
                leftSSCheckBox.Checked = false;
                rightSSCheckBox.Checked = false;

                //Strain
                halfStrainBtn.Enabled = false;
                fullStrainBtn.Enabled = false;
                doubleStrainBtn.Enabled = false;
                spdStrainBar.Enabled = false;
                #endregion
            }
            else if (nextStepComboBox.Text == "Strain")
            {
                #region Strain Enabled
                caliBtn.Enabled = false;

                //Mixing
                topMixBtn.Enabled = false;
                immerBtn.Enabled = false;
                spdMixBar.Enabled = false;
                mixTriturateBtn.Enabled = false;
                timeMixUpDown.Enabled = false;


                //Disruption Section
                disruptTabControl.Enabled = false;

                //Delivery Section
                halfMlBtn.Enabled = false;
                oneMlBtn.Enabled = false;
                twoMlBtn.Enabled = false;
                spdDelBar.Enabled = false;
                leftSSCheckBox.Enabled = false;
                rightSSCheckBox.Enabled = false;

                //Strain
                halfStrainBtn.Enabled = true;
                fullStrainBtn.Enabled = true;
                doubleStrainBtn.Enabled = true;
                spdStrainBar.Enabled = true;
                #endregion
            }
            else if (nextStepComboBox.Text == "Maintenance")
            {
                #region Maintenance Enabled
                caliBtn.Enabled = true;

                //Mixing
                topMixBtn.Enabled = false;
                immerBtn.Enabled = false;
                spdMixBar.Enabled = false;
                mixTriturateBtn.Enabled = false;
                timeMixUpDown.Enabled = false;

                //Disruption Section
                disruptTabControl.Enabled = false;

                //Delivery Section
                halfMlBtn.Enabled = false;
                oneMlBtn.Enabled = false;
                twoMlBtn.Enabled = false;
                spdDelBar.Enabled = false;
                leftSSCheckBox.Enabled = false;
                rightSSCheckBox.Enabled = false;

                //Strain
                halfStrainBtn.Enabled = false;
                fullStrainBtn.Enabled = false;
                doubleStrainBtn.Enabled = false;
                spdStrainBar.Enabled = false;
                #endregion
            }


        }

        #endregion

        #region Run & Abort & Refresh
        private void runBtn_Click(object sender, EventArgs e)
        {
           myDialogResult = "run";
           this.Hide();
        }

        private void abortBtn_Click(object sender, EventArgs e)
        {
            myDialogResult = "abort";
            myCase = 5;
            myTest = 5;
            this.Hide();
        }

        #endregion

        //figure out for future
        #region Bump

        public EventHandler<BumpScriptEventArgs2> BumpScriptEvent2;

        

        public class BumpScriptEventArgs2 : EventArgs
        {
            public BumpScriptEventArgs2(int newState)
            {
                NewState = newState;
            }
            public int NewState { get; set; }
        }

        public void BumpScript(int newState)
        {
            BumpScriptEvent2?.Invoke(this, new BumpScriptEventArgs2(newState));
        }











        #endregion

        #region Display Functionality 
        

        #endregion




    }
}
