//=========================================================================================================
//
//                     Protocol Explorer Speeds
//                      
// user sets desired temp within cart, not block
// add FFPE, Cell, Nuclei feature
// redesign temp control section
//=========================================================================================================

using System;
using System.Threading;
using System.Linq;
using System.Collections.Generic;
using MAS.Framework.Logging;
using System.Windows;
using System.Windows.Forms;
using SerialPortLib;
using CustomMssgBox;
using OmegaScript.Scripts;

namespace OmegaScript
{
    public partial class OmegaScript : MarshalByRefObject, OmegaScriptCommon.IOmegaScript
    {
        bool ZmqSupport = false; //Required

        CustomMssgBox.CustomMssgBox cusMsgBox = new CustomMssgBox.CustomMssgBox();

        public panelControlerFFPE ffpePanel = new panelControlerFFPE();
        public strainHalf halfQues = new strainHalf();

        //public string myCommand = "null";
        int myCheck = 99;
        int caseState = 99;


        Instrument myInstrument;
        Cavro myFluidics;
        Sphincter verticalStage;

        int ScriptRunTimeSeconds = 330;
		int ScriptInitTimeSeconds = 30;

        DateTime ScriptStartTime;

        Random randomSec = new Random();

        int ProtocolStepNum = 0;

        public int MachineState = 0;

        public bool safeMode = false;
        public bool notified = false;
        public bool showPanel = false;

        
        //int incubTime = ffpePanel.incubationTime;

        public void ScriptRun()
        {
            //State Machine: 0:Idle / 1:Initializing / 2:Controlling Temp Down / 3: Controlling Temp Up / 4:Running Protocol / 5:Aborting / 6:Error / 7:Terminating

            //System.Diagnostics.Debugger.Launch();

            myInstrument = new Instrument(this);
            verticalStage = new Sphincter(this);
            
            AbortRequestEvent += AbortRequestEventHandler;
            BumpScriptEvent += BumpScriptEventHandler;
            SetParametersEvent += SetParametersEventHandler;

            MachineState = 1; //Initialize
            //ScriptLog(Severity.Control, "myCommand: " + myCommand);
            LogStateChange(0, 1);
            //ScriptLog(Severity.Control, "myCommand: " + myCommand);
            //ScriptLog(Severity.Control, "myCheck: " + myCheck);
            Initialize(ScriptInitTimeSeconds);

            //myCommand = ffpePanel.myDialogResult;
            myCheck = ffpePanel.myTest;
            caseState = ffpePanel.myCase;            

            caseState = 0;

            while (caseState != 99)
            {
                switch (caseState)
                {
                    case 0: //idle
                        ScriptLog(Severity.Control, "MS: Idling");
                        //ffpePanel = new panelControlerFFPE();
                        myInstrument.incStarted = false;
                        myInstrument.incFinished = false;
                        myInstrument.monitoringTempBlock();
                        ffpePanel.ShowDialog();
                        //myCommand = ffpePanel.myDialogResult;
                        myCheck = ffpePanel.myTest;
                        caseState = ffpePanel.myCase;
                       
                        //Idle();
                        break;
                    //case 1: //init
                    //    ScriptLog(Severity.Control, "MS: Initializing");
                    //    Initialize(ScriptInitTimeSeconds);
                    //    break;
                    case 2: //controlling temp down
                        //ScriptLog(Severity.Control, "MS: Cooling");
                        //ControlTemperatureCool();
                        break;
                    case 3: 
                        ScriptLog(Severity.Control, $"^ MS: Setting Temp Block to {ffpePanel.setTempBlock} Celsius");                    
                        ControlTemperatureFlexer();
                        ScriptLog(Severity.Control, "v");
                        caseState = 0;
                        break;
                    case 5: //aborting
                        ScriptLog(Severity.Control, "^ MS: Exiting Explorer");
                        myInstrument.explorerAbort();
                        ScriptLog(Severity.Control, "v Till Next Time ;)");
                        caseState = 99;
                        
                        break;
                    case 6: //calibration
                        ScriptLog(Severity.Control, "^ MS: Maintenance: Calibration");                        
                        myInstrument.Calibrate();
                        ScriptLog(Severity.Control, $"| Finshed Calibration with an offset of {grinderVertOffset}");
                        ScriptLog(Severity.Control, "v");
                        caseState = 0;
                        break;
                    case 7: //Mixing
                        ScriptLog(Severity.Control, "^ MS: Mixing/Incubation");
                        myInstrument.EngageCartridge();
                        myInstrument.incStarted = true;
                        myInstrument.ControlInsTemp_ProtoExplorer(ffpePanel.tempType, ffpePanel.incubationTime, ffpePanel.setTempBlock);                       
                        myInstrument.incubationExplorer(myCheck, ffpePanel.incubationTime, ffpePanel.setMixSpd, ffpePanel.whichCatch);
                        myInstrument.incFinished = true;
                        
                        ScriptLog(Severity.Control, "v Completed Mixing/Incubation");
                        caseState = 11;
                        break;
                    case 8: //Disruption
                        ScriptLog(Severity.Control, "^ MS: Disruption");
                        //foreach(var step in ffpePanel.customZProfile)
                        //{
                        //    ScriptLog(Severity.Info, $"vertSteps : {Convert.ToInt32(step)}");
                        //}
                        myInstrument.EngageCartridge();
                        myInstrument.monitoringTempBlock();
                        Disrupt(myCheck, ffpePanel.myAuto);
                        ScriptLog(Severity.Control, "v Completed Disruption");
                        caseState = 11;
                        break;
                    case 9: //Delivery
                        ScriptLog(Severity.Control, "^ MS: Delivery");
                        myInstrument.monitoringTempBlock();
                        Deliver(myCheck); //add conditionals
                        ScriptLog(Severity.Control, "v Completed Delivery");
                        caseState = 0;
                        break;
                    case 10: //Strain
                        ScriptLog(Severity.Control, "^ MS: Straining");
                        myInstrument.monitoringTempBlock();
                        myInstrument.EngageCartridge();
                        //ScriptLog(Severity.Control, "halfQues.whichOne: " + ffpePanel.whichCatch); // halfQues.whichOne);
                        Strain(myCheck);
                        ScriptLog(Severity.Control, "v Completed Straining");
                        caseState = 11;
                        break;
                    case 11:
                        ScriptLog(Severity.Control, "^ MS: Device Clean Up");
                        myInstrument.DisengageCartridge();
                        myInstrument.explorerEndRun();
                        ScriptLog(Severity.Control, "v");
                        caseState = 0;
                        break;
                    case 12:
                        ScriptLog(Severity.Control, "^ MS: Turning Temperature Controller Off.. ");
                        myInstrument.StopTemp();
                        ScriptLog(Severity.Control, "v Turned Temperature Controller Off.. ");
                        caseState = 0;
                        break;
                }
            }
            //myInstrument.explorerAbort();
            return;
            


            #region old Cases
            /*
            while (MachineState != 99)
            {
                switch (MachineState)
                {
                    case 0: //idle
                        ScriptLog(Severity.Control, "MS: Idling");
                        Idle();
                        
                        break;
                    case 1: //initializing
                        ScriptLog(Severity.Control, "Script Ver 2.4.25");
                        ScriptLog(Severity.Control, "MS: Initializing");
                        Initialize(ScriptInitTimeSeconds);
                        break;
                    case 2: //controlling temp down
                        ScriptLog(Severity.Control, "MS: Cooling");
                        ControlTemperatureCool();
                        break;
                    case 3: //controlling temp up
                        ScriptLog(Severity.Control, "MS: Heating");
                        ControlTemperatureHeat();
                        break;
                    case 4: //running protocol
                        ScriptLog(Severity.Control, "MS: Running");
                        RunProtocol();
                        break;
                    case 5: //aborting
                        ScriptLog(Severity.Control, "MS: Aborting");
                        PerformAbort();
                        break;
                    case 6: //maintenance all lines clean
                        ScriptLog(Severity.Control, "MS: Maintenance: All Lines Clean");
                        myInstrument.lineInput = "All";
                        myInstrument.maintenanceTask = "Clean";
                        ScriptLog(Severity.Control, "Expected: " + myInstrument.CalcMaintenanceTime());
                        RunMaintenance();
                        break;
                    case 7: //maintenance all lines clean
                        ScriptLog(Severity.Control, "MS: Maintenance: Cell Lines Clean");
                        myInstrument.lineInput = "Cells";
                        myInstrument.maintenanceTask = "Clean";
                        ScriptLog(Severity.Control, "Expected: " + myInstrument.CalcMaintenanceTime());
                        RunMaintenance();
                        break;
                    case 8: //maintenance all lines clean
                        ScriptLog(Severity.Control, "MS: Maintenance: Nuclei Lines Clean");
                        myInstrument.lineInput = "Nuclei";
                        myInstrument.maintenanceTask = "Clean";
                        ScriptLog(Severity.Control, "Expected: " + myInstrument.CalcMaintenanceTime());
                        RunMaintenance();
                        break;
                    case 9: //maintenance all lines clean
                        ScriptLog(Severity.Control, "MS: Maintenance: All Lines Rinse");
                        myInstrument.lineInput = "All";
                        myInstrument.maintenanceTask = "Rinse";
                        ScriptLog(Severity.Control, "Expected: " + myInstrument.CalcMaintenanceTime());
                        RunMaintenance();
                        break;
                    case 10: //maintenance all lines clean
                        ScriptLog(Severity.Control, "MS: Maintenance: Cell Lines Rinse");
                        myInstrument.lineInput = "Cells";
                        myInstrument.maintenanceTask = "Rinse";
                        ScriptLog(Severity.Control, "Expected: " + myInstrument.CalcMaintenanceTime());
                        RunMaintenance();
                        break;
                    case 11: //maintenance all lines clean
                        ScriptLog(Severity.Control, "MS: Maintenance: Nuclei Lines Rinse");
                        myInstrument.lineInput = "Nuclei";
                        myInstrument.maintenanceTask = "Rinse";
                        ScriptLog(Severity.Control, "Expected: " + myInstrument.CalcMaintenanceTime());
                        RunMaintenance();
                        break;
                    case 12: //maintenance instrument diagnostics
                        ScriptLog(Severity.Control, "MS: Maintenance: Instrument Diagnostics");
                        myInstrument.maintenanceTask = "Diagnostics";
                        ScriptLog(Severity.Control, "Expected: " + myInstrument.CalcMaintenanceTime());
                        RunMaintenance();
                        break;
                    case 13:
                        ScriptLog(Severity.Control, "MS: Maintenance: Instrument Calibration");
                        myInstrument.maintenanceTask = "Calibration";
                        ScriptLog(Severity.Control, "Expected: " + myInstrument.CalcMaintenanceTime());
                        RunMaintenance();
                        break;
                    case 14:
                        ScriptLog(Severity.Control, "MS: Unclaimed");
                        Idle();
                        break;
                    case 15:
                        ScriptLog(Severity.Control, "MS: Shutting Down");
                        ScriptLog(Severity.Info, "Shutting Down");
                        //Idle();
                        //myInstrument.StopTemp();
                        break;
                    case 16:
                        ScriptLog(Severity.Control, "MS: Safe Mode-Disconnected");
                        ScriptLog(Severity.Info, "Safe Mode-Disconnected");
                        Idle();
                        break;
                    case 17:
                        ScriptLog(Severity.Control, "MS: Safe Mode-Instrument Off");
                        ScriptLog(Severity.Info, "Safe Mode-Instrument Off");
                        Idle();
                        break;
                    case 18:
                        ScriptLog(Severity.Control, "MS: Safe Mode-Malfunction");
                        ScriptLog(Severity.Info, "Safe Mode-Malfunction");
                        Idle();
                        break;
                    case 98: //error
                        ScriptLog(Severity.Control, "MS: Error");
                        Idle();
                        break;
                }
            }         
            return;
            */
            #endregion
        }

        #region State Machines

        private void Initialize(int expectedInitTime)
        {

            //ScriptLog(Severity.Control, "Expected: " + expectedInitTime.ToString());

            //We can get an abort request

            if (abortRequested)
            {
                abortRequested = false; //clear it
                LogStateChange(1, 5);
                MachineState = 5;

            }
            else if (MachineState == 1)
            {
                Thread.Sleep(100);
                ScriptLog(Severity.Control, "Expected: " + myInstrument.CalcInitTime().ToString());

                ScriptLog(Severity.Control, "Step: Connecting...");
                myInstrument.SetupOffset(grinderVertOffset);
                myInstrument.CheckCOMS();
                myInstrument.SetupCOMS(this);
                myInstrument.Setup(this);
                ScriptLog(Severity.Control, "Step: Initializing...");
                myInstrument.Init();
                if (myInstrument.comsFine)
                {
                    LogStateChange(1, 0);
                    MachineState = 0; //go to temperature control state if initialization finished and no state change request
                }
                else
                {
                    if (myInstrument.comErrorShort == "disconnected")
                    {
                        LogStateChange(1, 16);
                        MachineState = 16; //go to temperature control state if initialization finished and no state change request
                    }
                    if (myInstrument.comErrorShort == "off")
                    {
                        LogStateChange(1, 17);
                        MachineState = 17; //go to temperature control state if initialization finished and no state change request
                    }
                    if (myInstrument.comErrorShort == "malfunction")
                    {
                        LogStateChange(1, 18);
                        MachineState = 18; //go to temperature control state if initialization finished and no state change request
                    }
                }
            }
            //Note: other transitions are blocked in BumpScriptEventHandler
        }

        private void Idle()
        {
            //We can get an init request, a temp control request, or a run request (all events)
            myInstrument.StopTemp();
            myInstrument.atTemp = false;
            if (tempOffFlag)
            {
                tempOffFlag = false;
                //FIXTHIS
                cusMsgBox.Show("Cooling turned off due to inactivity.", "Cooling Off", MessageBoxButtons.OK);
                //MessageBox.Show("Cooling turned off due to inactivity.", "Cooling Off",MessageBoxButtons.OK);
                
            }
            while (MachineState == 0 || (MachineState > 14 && MachineState < 19))
            {
                Thread.Sleep(100);
            }

            //LogStateChange(0, MachineState); // all transitions are legal

        }

        public void displayPanel(bool showPanel)
        {
            while (showPanel && myCheck == 99)
            {
                ffpePanel = new panelControlerFFPE();
                ffpePanel.ShowDialog();
                //myCommand = ffpePanel.myDialogResult;
                showPanel = false; 
            }
        }

        private void ControlTemperatureFlexer()
        {
            //We can get a new temp control request (heat, cool, stop) or a run request
            if (myInstrument.comsFine)
            {
                ScriptLog(Severity.Control, "| COMS are good");
                flexControlTemperature();
                
            }
            else
            {
                ScriptLog(Severity.Control, "Expected: " + 10);
                myInstrument.ReportCOMError();
                LogStateChange(2, 0);
                caseState = 0;
            }
        }

        private void ControlTemperatureCool()
        {
            //We can get a new temp control request (heat, cool, stop) or a run request
            if (myInstrument.comsFine)
            {
                ScriptLog(Severity.Control, "COMS are good");
                ControlTemperature(2);
            }
            else
            {
                ScriptLog(Severity.Control, "Expected: " + 10);
                myInstrument.ReportCOMError();
                LogStateChange(2, 0);
                caseState = 0;
            }
        }

        private void ControlTemperatureHeat()
        {
            //We can get a new temp control request (heat, cool, stop) or a run request
            ScriptLog(Severity.Info, "Got into ControlTempHeat() ");
            if (myInstrument.comsFine)
            {
                ScriptLog(Severity.Info, "Got into ControlTempHeat() if");
                ControlTemperature(3);
            }
            else
            {
                ScriptLog(Severity.Control, "Expected: " + 10);
                myInstrument.ReportCOMError();
                LogStateChange(3, 0);
                caseState = 0;
                
            }
        }

        private void RunProtocol()
        {
            //We can get an abort request
            //NOTE: state change requests while running are ignored; use Abort to stop a run

            //MachineState = 4;
            ScriptLog(Severity.Control, "Script v 2.2.0.0");
            ScriptLog(Severity.Control, "Executing Protocol...");
            //===========================================================================================
            //First refresh the protocol parameters in case the host sent us new ones:
            RefreshProtocolParameters();
            TranslateParameters();
            //===========================================================================================

            int washNum;
            if (runOutput == "Cells")
                washNum = 2;
            else
                washNum = 1;

            ScriptStartTime = DateTime.Now;

            ScriptLog(Severity.Control, "Expected: " + myInstrument.CalcRunTime().ToString());

            myInstrument.ControlInsTemp(); if (abortRequested) goto Aborted;

            myInstrument.PrimePrimary(); if (abortRequested) goto Aborted;
            myInstrument.EngageCartridge(); if (abortRequested) goto Aborted;
            myInstrument.WaitFluidicAndEngageAsync(); if (abortRequested) goto Aborted;

            myInstrument.Deliver(); if (abortRequested) goto Aborted;

            myInstrument.PredisruptSample(); if (abortRequested) goto Aborted;

            if (runOutput == "Cells")
            {

                if (tissue == "Lung")
                {

                    myInstrument.Incubate(); if (abortRequested) goto Aborted;

                    myInstrument.startTime = DateTime.UtcNow;
                    myInstrument.Disrupt(); if (abortRequested) goto Aborted;
                    myInstrument.endTime = DateTime.UtcNow;
                    myInstrument.ExtendRunTime(myInstrument.disruptAndPrimeTime);
                }

                myInstrument.Incubate(); if (abortRequested) goto Aborted;

                myInstrument.incFinished = true;

                myInstrument.PrimeSecondary(); if (abortRequested) goto Aborted;

                myInstrument.Disrupt(); if (abortRequested) goto Aborted;
            }

            else //runOutput == "Nuclei"
            {
                //just planning for extended protocol right now, will worry about different combos later
                if (disruptCycles > 1)
                {
                    for (int i = 1; i < disruptCycles; i++)
                    {
                        //ScriptLog(Severity.Control, "Nuclei Disruption Loop: " + i + "Nuclei Disruption Cycle: " + disruptCycles);
                        myInstrument.Disrupt(); if (abortRequested) goto Aborted;
                        myInstrument.Incubate(); if (abortRequested) goto Aborted;
                    }
                }
                else
                {
                    myInstrument.incStarted = true;
                }

                myInstrument.incFinished = true;

                myInstrument.PrimeSecondary(); if (abortRequested) goto Aborted;

                myInstrument.Disrupt(); if (abortRequested) goto Aborted;

            }

            myInstrument.WaitFluidicAndDisruptAsync(); if (abortRequested) goto Aborted;

            myInstrument.Strain(); if (abortRequested) goto Aborted;

            for (int d = 0; d < washNum; d++)
            {
                myInstrument.Wash(); if (abortRequested) goto Aborted;
                myInstrument.Strain(); if (abortRequested) goto Aborted;
            }

            myInstrument.TempBackToZero(); if (abortRequested) goto Aborted;

            myInstrument.DisengageCartridge(); if (abortRequested) goto Aborted;

            myInstrument.WaitTempAsync(); if (abortRequested) goto Aborted;

            myInstrument.EndRun(); if (abortRequested) goto Aborted;

            myInstrument.ResetFlags();

            ScriptLog(Severity.Info, "Run Finished");
            ScriptLog(Severity.Control, "Run Finished @" + DateTime.UtcNow);


        //////////////////////////////////////////////////////
        Aborted:
            if (abortRequested)
            {
                ScriptLog(Severity.Info, "Aborting Protocol");
                LogStateChange(4, 5);
                MachineState = 5;
                PerformAbort();
                //abortRequested = false;
                ////myInstrument.localAbort = false;
                //myInstrument.Abort();
                ScriptLog(Severity.Info, "Protocol Run Aborted");
                LogStateChange(5, 0);
                MachineState = 0;
            }
            else if (((Temperature)pParams.ControlParameters.IncubationTemp).ToString() == "Cool" && myInstrument.comsFine)
            {
                LogStateChange(4, 2);
                MachineState = 2; //go to idle state

            }
            else
            {
                LogStateChange(4, 0);
                MachineState = 0; //go to idle state
            }
            /////////////////////////////////////////////////////

            return;

        }

        public void Disrupt(int check, bool auto)
        {
            if (auto)
            {
                myInstrument.PredisruptSample(); //if (abortRequested) goto Aborted;
                ffpePanel.myAuto = false;
            }
            if (check < 25)
            {
                ScriptLog(Severity.Control, $"| Disruption Speed: {ffpePanel.setDisrupSpd} rpm");
            }
            if (check == 20)
            {
                ScriptLog(Severity.Control, "| Carrying out Default Cell Disruption");
                myInstrument.CellDefaultDisrupt(ffpePanel.setDisrupSpd, "other");
            }
            else if (check == 21)
            {
                ScriptLog(Severity.Control, "| Carrying out Cell Tritration Disruption");
                myInstrument.CellTritDisrupt(ffpePanel.setDisrupSpd);          
            }
            else if (check == 23)
            {
                ScriptLog(Severity.Control, "| Carrying out Cell Lung Disruption");
                myInstrument.CellDefaultDisrupt(ffpePanel.setDisrupSpd, "Lung");
            }
            else if (check == 24)
            {
                ScriptLog(Severity.Control, "| Carrying out Default Nuclei Disruption");
                myInstrument.NucleiDefaultDisrupt(ffpePanel.setDisrupSpd);
            }
            else if (check == 25)
            {
                ScriptLog(Severity.Control, "| Carrying out Dounce Disruption");
                myInstrument.NucleiDounceDisrupt(ffpePanel.setDisrupSpd, ffpePanel.whichCatch); //speed relates to the vertStage not the robo
            }
            else if (check == 26)
            {
                ScriptLog(Severity.Control, "| Carrying out Custome Disruption"); 
                myInstrument.FactorOffset(ffpePanel.customZProfile);
                myInstrument.customeDisruptProfile(ffpePanel.customZProfile, ffpePanel.customRoboProfile, ffpePanel.setDisrupSpd);

            }
            
        }

        public void Deliver(int check)
        {
            while (ffpePanel.selectedSS)
            {
                if (29 < check && check < 34) //remove conditional and check
                {
                    string task;
                    if (check == 30)
                    {
                        task = "0.5";
                    }
                    else if (check == 31)
                    {
                        task = "1";
                    }
                    else if (check == 32)
                    {
                        task = "2";
                    }
                    else
                    {
                        task = Convert.ToString(ffpePanel.desiredAmt);
                    }

                    ScriptLog(Severity.Control, $"|- Delivering {task} mL");
                    myInstrument.explorerDelivery(ffpePanel.valvePos, ffpePanel.solutionDeliver, ffpePanel.airPrime, ffpePanel.setDeliverySpd);
                    ScriptLog(Severity.Control, $"|- Delivered {task} mL of Solution...");
                    ffpePanel.selectedSS = false;

                    //myFluidics.Prime(ffpePanel.valvePos);

                    //myFluidics.flexDeliver(ffpePanel.solutionDeliver, ffpePanel.airPrime); //ffpePanel.valvePos

                    //?????????????????????????????????????????????????????????????????????????????????????
                    //Remove the first parameter and then remove the valve movement within to reduce threads
                }
            }
            //while (!ffpePanel.selectedSS)
            //{
            //    DialogResult userError = MessageBox.Show("Must Select a Single-Shot Position", "S.S Position", MessageBoxButtons.OK);
            //    if (userError == DialogResult.OK)
            //    {
            //        caseState = 0;
            //    }
            //}
        }

        public void Strain(int check)
        {
            //ScriptLog(Severity.Control, $"")
            if (check == 42)
            {
                ScriptLog(Severity.Control, "| Preforming a Double Strain...");
                for (int i = 0; i < 2; i++ )
                {
                    
                    myInstrument.explorerStrain(ffpePanel.strainSteps, check, ffpePanel.whichCatch, ffpePanel.setStrainSpd);
                }
            }
            else
            {
                ScriptLog(Severity.Control, $"| Preforming a {ffpePanel.myDialogResult}");
                myInstrument.explorerStrain(ffpePanel.strainSteps, check, ffpePanel.whichCatch, ffpePanel.setStrainSpd);
            }
        }

        private void RunMaintenance()
        {
            bool continueMaintenance = true;
            if (myInstrument.comsFine)
            {
                Thread.Sleep(100);
                //ScriptLog(Severity.Control, "Expected: " + myInstrument.CalcMaintTime(myInstrument.maintenanceTask, myInstrument.lineInput).ToString());

                var maintStartTime = DateTime.UtcNow;
                continueMaintenance = myInstrument.InstructUser(myInstrument.maintenanceTask, myInstrument.lineInput);
                var maintEndTime = DateTime.UtcNow;
                ScriptLog(Severity.Control, "Extend: " + Convert.ToInt32((maintEndTime - maintStartTime).TotalSeconds));
                if (continueMaintenance)
                {
                    if (myInstrument.maintenanceTask == "Clean")
                    {
                        //Clear Lines
                        myInstrument.ClearLines(myInstrument.lineInput);
                        //Clean Lines
                        myInstrument.CleanLines(myInstrument.lineInput);
                        for (int i = 0; i < 4; i++)
                        {
                            //Flush Lines
                            myInstrument.RinseLines(myInstrument.lineInput);
                            //Clear Lines
                            myInstrument.ClearLines2(myInstrument.lineInput);
                            //Repeat Flush and Clear 4x
                        }
                        //FIXTHIS
                        cusMsgBox.Show("Cleaning Complete.", "Cleaning Complete", MessageBoxButtons.OK);
                        //MessageBox.Show("Cleaning Complete.", "Cleaning Complete", MessageBoxButtons.OK);
                        ScriptLog(Severity.Info, "Cleaning Complete.");
                    }
                    else if (myInstrument.maintenanceTask == "Rinse")
                    {
                        //Clear Lines
                        myInstrument.ClearLines(myInstrument.lineInput);
                        //Flush Lines
                        myInstrument.RinseLines(myInstrument.lineInput);
                        //Clear Lines
                        myInstrument.ClearLines(myInstrument.lineInput);
                        //FIXTHIS
                        cusMsgBox.Show("Rinse Complete.", "Rinse Complete", MessageBoxButtons.OK);
                        //MessageBox.Show("Rinse Complete.", "Rinse Complete", MessageBoxButtons.OK);
                        ScriptLog(Severity.Info, "Rinse Complete.");
                    }
                    else if (myInstrument.maintenanceTask == "Diagnostics")
                    {

                        myInstrument.StepperDiagnostics();
                        myInstrument.DCDiagnostics();
                        myInstrument.FluidicDiagnostics();
                        myInstrument.TempDiagnostics();
                        myInstrument.SummarizeResults();
                    }
                    else if (myInstrument.maintenanceTask == "Calibration")
                    {
                        myInstrument.SetupOffset(-grinderVertOffset);
                        myInstrument.Calibrate();
                        myInstrument.SetupOffset(grinderVertOffset);
                    }
                    else
                    {
                        myInstrument.ReportCOMError();
                    }
                }
            }
            LogStateChange(MachineState, 0);
            MachineState = 0;
        }

        private void PerformAbort()
        {
            //MachineState = 5;
            ScriptLog(Severity.Info, "Performing Abort");
            ScriptLog(Severity.Control, "Expected: " + myInstrument.CalcAbortTime());
            //AbortCleanup();

            ScriptLog(Severity.Info, "Performing Abort Cleanup");
            //myInstrument.TempBackToZero();
            //myInstrument.WaitTempAsync();
            abortRequested = false;
            myInstrument.Abort();
            myInstrument.ResetFlags();
            ScriptLog(Severity.Info, "Successfully Aborted");

            //ScriptLog(Severity.Info, "Cleanup was run");

            LogStateChange(5, 99);
            caseState = 99; //go to idle state
        }

        

        private void Error()
        {
            while (MachineState == 6)
            {
                Thread.Sleep(100);
            }

            //LogStateChange(6, MachineState);

        }

        #endregion

        #region Support Methods

        public void flexControlTemperature()
        {
            var startTime = DateTime.UtcNow;
            int expectedControlTime;
            int myMachineState;
            var mbTempCountdown = DateTime.UtcNow;
            notified = false;

            //while (caseState == myMachineState) //MS -> myCheck
            //{
            if (ffpePanel.setTempBlock > 20)
            {
                myMachineState = 3;
            }
            else
            {
                myMachineState = 2;
            }
            #region Control Temperature               
            expectedControlTime = myInstrument.flexTemp(ffpePanel.setTempBlock, ffpePanel.tempType);
            startTime = DateTime.UtcNow;
            #endregion

            #region Check Temperature
            myInstrument.atTemp = myInstrument.CheckTemp(myMachineState);
            if (myInstrument.atTemp)
            {
                //while (caseState == myMachineState || caseState != myMachineState)
                while (myMachineState == 3 || myMachineState == 2)
                {
                    if (!notified)
                    {
                        //ScriptLog(Severity.Control, $"Notified User 1, caseState: {caseState}, myMachineState: {myMachineState}, notified value {notified}");
                        ScriptLog(Severity.Control, "| Notification: Temperature Reached"); //inform the host
                        mbTempCountdown = DateTime.UtcNow;
                        //notified = true;
                        myMachineState = 0;
                        //myInstrument.atTemp = false;

                    }
                    myInstrument.ReportTemp();
                    //cool timeout function
                    //if (myCheck == 2 && notified && ((DateTime.UtcNow - mbTempCountdown).TotalMinutes > 30))
                    //{
                    //    myCheck = TempTimeOut();
                    //    if (myCheck == 2)
                    //    {
                    //        mbTempCountdown = DateTime.UtcNow;
                    //    }
                    //    else
                    //    {
                    //        LogStateChange(myMachineState, 0);
                    //    }
                    //}
                    //Thread.Sleep(5000);
                    ScriptLog(Severity.Control, "Time Elapsed (min): " + (DateTime.UtcNow - mbTempCountdown).TotalMinutes); //inform the host
                }
            }
            else
            {
                if (expectedControlTime < 10)
                {
                    expectedControlTime = 10;
                }
                ScriptLog(Severity.Control, $"| Expected: {(expectedControlTime/60).ToString()} mins");
                myInstrument.ReportTemp();

                //int loopCnt = 0;

                while ((myMachineState == 3 || myMachineState == 2) && !myInstrument.atTemp)
                {
                    Thread.Sleep(1000);
                    myInstrument.atTemp = myInstrument.CheckTemp(myMachineState);
                    myInstrument.ReportTemp();
                    //ScriptLog(Severity.Control, loopCnt.ToString());
                    //ScriptLog(Severity.Control, myMachineState.ToString());
                    //ScriptLog(Severity.Control, MachineState.ToString());
                    //loopCnt += 1;
                    //ScriptLog(Severity.Control, "Still Looping");
                    //if (loopCnt >= 200) atTemp = true;

                    if (myInstrument.atTemp)
                    {
                        while (myMachineState == 3 || myMachineState == 2)
                        {
                            if (!notified)
                            {
                                ScriptLog(Severity.Control, "| Notification: Temperature Reached"); //inform the host
                                mbTempCountdown = DateTime.UtcNow;
                                //notified = true;
                                myMachineState = 0;
                                //ScriptLog(Severity.Info, $"Notified User 2, caseState: {caseState}, myMachineState: {myMachineState}, notified value {notified}");
                            }
                            //cool timeout function
                            myInstrument.ReportTemp();
                            //cool timeout function
                            if (myMachineState == 2 && notified && ((DateTime.UtcNow - mbTempCountdown).TotalMinutes > 30))
                            {
                                myCheck = TempTimeOut();

                                if (myCheck == 2)
                                {
                                    mbTempCountdown = DateTime.UtcNow;
                                }
                                else
                                {
                                    LogStateChange(myMachineState, 0);
                                }
                            }
                            Thread.Sleep(5000);
                            ScriptLog(Severity.Control, "| Time Elapsed (min): " + (DateTime.UtcNow - mbTempCountdown).TotalMinutes); //inform the host
                        }
                    }
                    else
                    {
                        if (expectedControlTime - (DateTime.UtcNow - startTime).TotalSeconds <= 0)
                        {
                            expectedControlTime += 5;
                            //Thread.Sleep(1000);
                            ScriptLog(Severity.Control, "|-- Extend: 5 seconds");
                        }
                        //try
                        //{
                        //    int adjTime = myInstrument.AdjustTempTime(myMachineState);
                        //    ScriptLog(Severity.Control, "Extend: " + adjTime.ToString());
                        //}
                        //catch (Exception e)
                        //{
                        //    ScriptLog(Severity.Control, "Fuck: " + e.Message);
                        //}
                    }
                }
                while ((myMachineState == 3 || myMachineState == 2) && myInstrument.atTemp)
                {
                    myInstrument.ReportTemp();
                }
            }
            #endregion

            //}
        }

        public void ControlTemperature(int myMachineState)
        {
            var startTime = DateTime.UtcNow;
            int expectedControlTime;
            var mbTempCountdown = DateTime.UtcNow;
            notified = false;

            while (myCheck == myMachineState) //MS -> myCheck
            {
                #region Control Temperature
                if (myMachineState == 2)
                {
                    //we are cooling
                    ScriptLog(Severity.Control, "COOLING, State = " + myMachineState);
                    expectedControlTime = myInstrument.Cool();
                    startTime = DateTime.UtcNow;

                }
                else
                {
                    //we are heating
                    ScriptLog(Severity.Control, "HEATING, State = " + myMachineState);
                    expectedControlTime = myInstrument.Heat();
                    startTime = DateTime.UtcNow;
                }
                #endregion

                #region Check Temperature
                myInstrument.atTemp = myInstrument.CheckTemp(myMachineState);
                if (myInstrument.atTemp)
                {
                    while (myCheck == myMachineState)
                    {
                        if (!notified)
                        {
                            ScriptLog(Severity.Control, "Notification: Temperature Reached"); //inform the host
                            mbTempCountdown = DateTime.UtcNow;                            
                            notified = true;
                        }
                        myInstrument.ReportTemp();
                        //cool timeout function
                        if (myCheck == 2 && notified && ((DateTime.UtcNow - mbTempCountdown).TotalMinutes > 30))
                        {
                            myCheck = TempTimeOut();
                            if (myCheck == 2)
                            {
                                mbTempCountdown = DateTime.UtcNow;
                            }
                            else
                            {
                                LogStateChange(myMachineState, 0);
                            }
                        }
                        //Thread.Sleep(5000);
                        ScriptLog(Severity.Control, "Time Elapsed (min): " + (DateTime.UtcNow - mbTempCountdown).TotalMinutes); //inform the host
                    }
                }
                else
                {
                    if (expectedControlTime < 10)
                    {
                        expectedControlTime = 10;
                    }
                    ScriptLog(Severity.Control, "Expected: " + expectedControlTime.ToString());
                    myInstrument.ReportTemp();

                    //int loopCnt = 0;

                    while ((myCheck == myMachineState) && !myInstrument.atTemp)
                    {
                        Thread.Sleep(1000);
                        myInstrument.atTemp = myInstrument.CheckTemp(myMachineState);
                        myInstrument.ReportTemp();
                        //ScriptLog(Severity.Control, loopCnt.ToString());
                        //ScriptLog(Severity.Control, myMachineState.ToString());
                        //ScriptLog(Severity.Control, MachineState.ToString());
                        //loopCnt += 1;
                        //ScriptLog(Severity.Control, "Still Looping");
                        //if (loopCnt >= 200) atTemp = true;

                        if (myInstrument.atTemp)
                        {
                            while (myCheck == myMachineState)
                            {
                                if (!notified)
                                {
                                    ScriptLog(Severity.Control, "Notification: Temperature Reached"); //inform the host
                                    mbTempCountdown = DateTime.UtcNow;
                                    notified = true;
                                }
                                //cool timeout function
                                myInstrument.ReportTemp();
                                //cool timeout function
                                if (myCheck == 2 && notified && ((DateTime.UtcNow - mbTempCountdown).TotalMinutes > 30))
                                {
                                    myCheck = TempTimeOut();

                                    if (myCheck == 2)
                                    {
                                        mbTempCountdown = DateTime.UtcNow;
                                    }
                                    else
                                    {
                                        LogStateChange(myMachineState, 0);
                                    }
                                }
                                Thread.Sleep(5000);
                                ScriptLog(Severity.Control, "Time Elapsed (min): " + (DateTime.UtcNow - mbTempCountdown).TotalMinutes); //inform the host
                            }
                        }
                        else
                        {
                            if (expectedControlTime - (DateTime.UtcNow - startTime).TotalSeconds <= 0)
                            {
                                expectedControlTime += 5;
                                //Thread.Sleep(1000);
                                ScriptLog(Severity.Control, "Extend: 5");
                            }
                            //try
                            //{
                            //    int adjTime = myInstrument.AdjustTempTime(myMachineState);
                            //    ScriptLog(Severity.Control, "Extend: " + adjTime.ToString());
                            //}
                            //catch (Exception e)
                            //{
                            //    ScriptLog(Severity.Control, "Fuck: " + e.Message);
                            //}
                        }
                    }
                    while ((myCheck == myMachineState) && myInstrument.atTemp)
                    {
                        myInstrument.ReportTemp();
                    }
                }
                #endregion
            }
        }

        private void LogStateChange(int s0, int s1)
        {
            //It is important to log all state changes!

            //rlm 20-06-20 -- Do Not Report state change if no change!! <=====================================================================================
            if (s0 != s1)
                ScriptLog(Severity.Control, string.Format("State Change: {0} to {1}", s0, s1));
            if (s1 == 4)
            {
                Thread.Sleep(100);
                ScriptLog(Severity.Control, string.Format("State Change: {0} to {1}", s0, s1));
            }
            myInstrument.RecordData(s0, s1);
            myInstrument.StartMotorLogging(s1);
            myInstrument.UpdateTempDataInsState(s1);
        }

        private void MockStateChange(int s0, int s1)
        {
            //It is important to log all state changes!

            //rlm 20-06-20 -- Do Not Report state change if no change!! <=====================================================================================
            if (s0 != s1)
                ScriptLog(Severity.Control, string.Format("State Change: {0} to {1}", s0, s1));
            //myInstrument.RecordData(s0, s1);
            //myInstrument.StartMotorLogging(s1);
            //myInstrument.UpdateTempDataInsState(s1);
        }

        #endregion

        #region Event Handlers

        private void SetParametersEventHandler(object sender, EventArgs args)
        {
            //rlm 20-06-22 -- receive notification that parameters have changed
            RefreshProtocolParameters();
            //we can now take whatever action is needed (e.g. check new temperature set points)

            string myCellType = ((CellType)pParams.CellType).ToString();
            string tempChoice = ((Temperature)pParams.ControlParameters.IncubationTemp).ToString();
            if (MachineState == 14)
            {
                //do nothing
            }
            else
            {
                if (tempChoice == "Heat" && myInstrument.comsFine)
                {
                    LogStateChange(MachineState, 3);
                    MachineState = 3;
                }
                else if (tempChoice == "Cool" && myInstrument.comsFine)
                {
                    LogStateChange(MachineState, 2);
                    MachineState = 2;
                }
                else
                {
                    LogStateChange(MachineState, 0);
                    MachineState = 0;
                }
            }

        }

        public void BumpScriptEventHandler(object sender, BumpScriptEventArgs args)
        {
            //Change Machine State

            int newState = args.NewState;
            int currentState = MachineState;
            notified = false;

            //System.Windows.Forms.MessageBox.Show("State Requested: " + newState.ToString()); //just for demo
            ScriptLog(Severity.Control, "Current State: " + currentState.ToString());
            ScriptLog(Severity.Control, "State Requested: " + newState.ToString());

            if (MachineState == 1)// Veto any attempted state change other than abort while initializing!
            {
                if (newState != 5)
                    ScriptLog(Severity.Control, "State change rejected");
            }
            else if (MachineState == 4)// Veto any attempted state change other than abort while running!
            {
                if (newState != 5)
                    ScriptLog(Severity.Control, "State change rejected");
            }
            else if (MachineState == 6)// Veto any attempted state change other than initialize while in error state
            {
                if (newState != 1)
                    ScriptLog(Severity.Control, "State change rejected");
            }
            else if (MachineState == 16 || MachineState == 17 || MachineState == 18 )// Veto any attempted state change other than initialize while in safe mode state
            {
                if (newState == 4)
                {
                    ScriptLog(Severity.Control, "State change rejected");
                    //FIXTHIS
                    cusMsgBox.Show("Cannot Run in Disabled Mode.\nPlease check Instrument Status.\n ", "Check Instrument!", MessageBoxButtons.OK);
                    //MessageBox.Show("Cannot Run in Disabled Mode.\nPlease check Instrument Status.\n ", "Check Instrument!", MessageBoxButtons.OK);
                }
                else if (newState == 2 || newState == 3)
                {
                    ScriptLog(Severity.Control, "State change rejected");
                    //FIXTHIS
                    cusMsgBox.Show("Cannot Control Temp in Disabled Mode.\nPlease check Instrument Status.\n ", "Check Instrument!", MessageBoxButtons.OK);
                    //MessageBox.Show("Cannot Control Temp in Disabled Mode.\nPlease check Instrument Status.\n ", "Check Instrument!", MessageBoxButtons.OK);
                }
                else if (newState == 0)
                {
                    ScriptLog(Severity.Control, "State change rejected");
                }
                else if (newState != 15)
                {
                    ScriptLog(Severity.Control, "State change rejected");
                    //FIXTHIS
                    cusMsgBox.Show("Cannot Run in Disabled Mode.\nPlease check Instrument Status.\n ", "Check Instrument!", MessageBoxButtons.OK);
                    //MessageBox.Show("Cannot Run in Disabled Mode.\nPlease check Instrument Status.\n ", "Check Instrument!", MessageBoxButtons.OK);
                }
            }
            else if (MachineState == 15)// Veto any attempted state change while in shut down state
            {
                ScriptLog(Severity.Control, "State change rejected");
            }
            else
            {
                LogStateChange(MachineState, newState);
                MachineState = newState;
                ScriptLog(Severity.Control, "Success! New State = " + MachineState);
                if (MachineState == 15)
                {
                    ScriptLog(Severity.Control, "MS: Shutting Down");
                    ScriptLog(Severity.Info, "Shutting Down");
                }
            }
        }

        private void BumpScriptEventHandler2(object sender, panelControlerFFPE.BumpScriptEventArgs2 args)
        {
            //Change Machine State

            int newState = args.NewState;
            int currentState = MachineState;
            notified = false;

            //System.Windows.Forms.MessageBox.Show("State Requested: " + newState.ToString()); //just for demo
            ScriptLog(Severity.Control, "Current State: " + currentState.ToString());
            ScriptLog(Severity.Control, "State Requested: " + newState.ToString());

            if (MachineState == 1)// Veto any attempted state change other than abort while initializing!
            {
                if (newState != 5)
                    ScriptLog(Severity.Control, "State change rejected");
            }
            else if (MachineState == 4)// Veto any attempted state change other than abort while running!
            {
                if (newState != 5)
                    ScriptLog(Severity.Control, "State change rejected");
            }
            else if (MachineState == 6)// Veto any attempted state change other than initialize while in error state
            {
                if (newState != 1)
                    ScriptLog(Severity.Control, "State change rejected");
            }
            else if (MachineState == 16 || MachineState == 17 || MachineState == 18)// Veto any attempted state change other than initialize while in safe mode state
            {
                if (newState == 4)
                {
                    ScriptLog(Severity.Control, "State change rejected");
                    //FIXTHIS
                    cusMsgBox.Show("Cannot Run in Disabled Mode.\nPlease check Instrument Status.\n ", "Check Instrument!", MessageBoxButtons.OK);
                    //MessageBox.Show("Cannot Run in Disabled Mode.\nPlease check Instrument Status.\n ", "Check Instrument!", MessageBoxButtons.OK);
                }
                else if (newState == 2 || newState == 3)
                {
                    ScriptLog(Severity.Control, "State change rejected");
                    //FIXTHIS
                    cusMsgBox.Show("Cannot Control Temp in Disabled Mode.\nPlease check Instrument Status.\n ", "Check Instrument!", MessageBoxButtons.OK);
                    //MessageBox.Show("Cannot Control Temp in Disabled Mode.\nPlease check Instrument Status.\n ", "Check Instrument!", MessageBoxButtons.OK);
                }
                else if (newState == 0)
                {
                    ScriptLog(Severity.Control, "State change rejected");
                }
                else if (newState != 15)
                {
                    ScriptLog(Severity.Control, "State change rejected");
                    //FIXTHIS
                    cusMsgBox.Show("Cannot Run in Disabled Mode.\nPlease check Instrument Status.\n ", "Check Instrument!", MessageBoxButtons.OK);
                    //MessageBox.Show("Cannot Run in Disabled Mode.\nPlease check Instrument Status.\n ", "Check Instrument!", MessageBoxButtons.OK);
                }
            }
            else if (MachineState == 15)// Veto any attempted state change while in shut down state
            {
                ScriptLog(Severity.Control, "State change rejected");
            }
            else
            {
                LogStateChange(MachineState, newState);
                MachineState = newState;
                ScriptLog(Severity.Control, "Success! New State = " + MachineState);
                if (MachineState == 15)
                {
                    ScriptLog(Severity.Control, "MS: Shutting Down");
                    ScriptLog(Severity.Info, "Shutting Down");
                }
            }
        }

        private void AbortRequestEventHandler(object sender, EventArgs args)
        {
            //Take immediate action on abort, such as stopping the stepper motor
            //System.Windows.Forms.MessageBox.Show("Abort Requested"); //just for demo
            ScriptLog(Severity.Control, "Abort Requested");
            //LogStateChange(4, 5);
            //MachineState = 5;
        }

        private void PortReconnected()
        {
            //New event handler 3/12/21

            //log the reconnect here
            ScriptLog(Severity.Control, "TEMP CONTROLLER RECONNECTED!!!!!!");
            if (MachineState == 4)
            {
                //FIXTHIS
                cusMsgBox.Show("Oops. Looks like something went wrong.\nRun will abort.\nPlease restart instrument.\n ", "Warning", MessageBoxButtons.OK);
                //MessageBox.Show("Oops. Looks like something went wrong.\nRun will abort.\nPlease restart instrument.\n ", "Warning", MessageBoxButtons.OK);
                abortRequested = true;
            }
            else
            {
                if (MachineState == 3)
                {
                    myInstrument.SetTempParams(57.0);
                    myInstrument.Heat();
                }
                else if (MachineState == 2)
                {
                    myInstrument.SetTempParams(2.0);
                    myInstrument.Cool();
                }
                else
                {
                    myInstrument.SetTempParams(20.0);
                }
                //FIXTHIS
                cusMsgBox.Show("Oops. Looks like something went wrong.\nPlease restart instrument.\n ", "Warning", MessageBoxButtons.OK);
                //MessageBox.Show("Oops. Looks like something went wrong.\nPlease restart instrument.\n ", "Warning", MessageBoxButtons.OK);
            }
            //resend config parameters
        }

        #endregion

        #region Simulation Methods
        private void SimulateAbortTime(int sec)
        {
            //This just wastes time to simulate an aborting script

            ScriptLog(Severity.Control, "Expected: " + sec.ToString());

            for (int j = 0; j < sec; j++)
            {
                Thread.Sleep(1000);
            }
        }

        private void SimulateRunTime(int sec)
        {
            //This just wastes time to simulate a running script

            int entryState = MachineState; //entryState is the state on entry; if it changes, return to caller

            int sec10 = sec / 10; //update at 10 second intervals for this test script

            for (int i = 0; i < sec10; i++)
            {
                //Send a status update every 10 seconds

                for (int j = 0; j < 100; j++)
                {
                    Thread.Sleep(100); //check for abort every 100ms
                    if (abortRequested) return;
                    if (MachineState != entryState) return; //<==============================================
                }

                int randomDelay = randomSec.Next(1, 10);
                Thread.Sleep(randomDelay * 1000); //simulate an unexpected delay
                ScriptLog(Severity.Control, "Extend: " + randomDelay.ToString()); //extend the expected runtime by 2 seconds -- just for testing
            }
        }

        #endregion

        //private void AbortCleanup()
        //{
        //    ScriptLog(Severity.Info, "Performing Abort Cleanup");
        //    myInstrument.WaitTempAsync();
        //    abortRequested = false;
        //    myInstrument.Abort();
        //    myInstrument.ResetFlags();
        //}

    }
}