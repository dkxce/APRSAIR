using System.Collections.Generic;
using System.Text.RegularExpressions;

public class DtmfGenerator
{
        public const int NUMBER_BUTTONS = 33;
        static short [] tempCoeff = {27980, 26956, 25701, 24218, 19073, 16325, 13085, 9315};
        int countDurationPushButton;
        int countDurationPause;
        int tempCountDurationPushButton;
        int tempCountDurationPause;
        bool readyFlag;    
        char [] pushDialButtons = new char[NUMBER_BUTTONS];
        int countLengthDialButtonsArray;
        int count;
        int sizeOfFrame;

        short tempCoeff1, tempCoeff2;
        int y1_1, y1_2, y2_1, y2_2;
        
        /** 
            FrameSize - The frame's size of the 8 kHz output samples 
            DurationPush - tone's duration (ms)
            DurationPause - pause's duration between tones (ms)
            public DtmfGenerator(int FrameSize, int DurationPush, int DurationPause)
        */
	public DtmfGenerator(int frameSize, int durationPush, int durationPause)
        {
            countDurationPushButton = (durationPush << 3)/frameSize + 1;
            countDurationPause = (durationPause << 3)/frameSize + 1;
            sizeOfFrame = frameSize;
            readyFlag = true;
            countLengthDialButtonsArray = 0;
        }
	
	/**
            That function will be run on each outcoming frame
            public void dtmfGenerating(short [] y);
         */
	public void dtmfGenerating(short []y)
        {
             if(readyFlag)   return;

             while(countLengthDialButtonsArray > 0)
              {
                if(countDurationPushButton == tempCountDurationPushButton)
                 {
                  switch(pushDialButtons[count])
                   {
                    case '1': tempCoeff1 = tempCoeff[0]; 
                              tempCoeff2 = tempCoeff[4];
                              y1_1 = tempCoeff[0];
                              y2_1 = 31000;
                              y1_2 = tempCoeff[4];
                              y2_2 = 31000; 
                              break;
                    case '2': tempCoeff1 = tempCoeff[0]; 
                              tempCoeff2 = tempCoeff[5];
                              y1_1 = tempCoeff[0];
                              y2_1 = 31000;
                              y1_2 = tempCoeff[5];
                              y2_2 = 31000;                    
                              break;
                    case '3': tempCoeff1 = tempCoeff[0]; 
                              tempCoeff2 = tempCoeff[6];
                              y1_1 = tempCoeff[0];
                              y2_1 = 31000;
                              y1_2 = tempCoeff[6];
                              y2_2 = 31000;                    
                              break;
                    case 'A': tempCoeff1 = tempCoeff[0]; 
                              tempCoeff2 = tempCoeff[7];
                              y1_1 = tempCoeff[0];
                              y2_1 = 31000;
                              y1_2 = tempCoeff[7];
                              y2_2 = 31000;                    
                              break;
                    case '4': tempCoeff1 = tempCoeff[1]; 
                              tempCoeff2 = tempCoeff[4];
                              y1_1 = tempCoeff[1];
                              y2_1 = 31000;
                              y1_2 = tempCoeff[4];
                              y2_2 = 31000;                    
                              break;
                    case '5': tempCoeff1 = tempCoeff[1]; 
                              tempCoeff2 = tempCoeff[5];
                              y1_1 = tempCoeff[1];
                              y2_1 = 31000;
                              y1_2 = tempCoeff[5];
                              y2_2 = 31000; 
                              break;
                    case '6': tempCoeff1 = tempCoeff[1]; 
                              tempCoeff2 = tempCoeff[6];
                              y1_1 = tempCoeff[1];
                              y2_1 = 31000;
                              y1_2 = tempCoeff[6];
                              y2_2 = 31000; 
                              break;
                    case 'B': tempCoeff1 = tempCoeff[1]; 
                              tempCoeff2 = tempCoeff[7];
                              y1_1 = tempCoeff[1];
                              y2_1 = 31000;
                              y1_2 = tempCoeff[7];
                              y2_2 = 31000; 
                              break;
                    case '7': tempCoeff1 = tempCoeff[2]; 
                              tempCoeff2 = tempCoeff[4];
                              y1_1 = tempCoeff[2];
                              y2_1 = 31000;
                              y1_2 = tempCoeff[4];
                              y2_2 = 31000; 
                              break;
                    case '8': tempCoeff1 = tempCoeff[2]; 
                              tempCoeff2 = tempCoeff[5];
                              y1_1 = tempCoeff[2];
                              y2_1 = 31000;
                              y1_2 = tempCoeff[5];
                              y2_2 = 31000; 
                              break;
                    case '9': tempCoeff1 = tempCoeff[2]; 
                              tempCoeff2 = tempCoeff[6];
                              y1_1 = tempCoeff[2];
                              y2_1 = 31000;
                              y1_2 = tempCoeff[6];
                              y2_2 = 31000; 
                              break;
                    case 'C': tempCoeff1 = tempCoeff[2]; 
                              tempCoeff2 = tempCoeff[7];
                              y1_1 = tempCoeff[2];
                              y2_1 = 31000;
                              y1_2 = tempCoeff[7];
                              y2_2 = 31000; 
                              break;
                    case '*': tempCoeff1 = tempCoeff[3]; 
                              tempCoeff2 = tempCoeff[4];
                              y1_1 = tempCoeff[3];
                              y2_1 = 31000;
                              y1_2 = tempCoeff[4];
                              y2_2 = 31000; 
                              break;
                    case '0': tempCoeff1 = tempCoeff[3]; 
                              tempCoeff2 = tempCoeff[5];
                              y1_1 = tempCoeff[3];
                              y2_1 = 31000;
                              y1_2 = tempCoeff[5];
                              y2_2 = 31000; 
                              break;
                    case '#': tempCoeff1 = tempCoeff[3]; 
                              tempCoeff2 = tempCoeff[6];
                              y1_1 = tempCoeff[3];
                              y2_1 = 31000;
                              y1_2 = tempCoeff[6];
                              y2_2 = 31000; 
                              break;
                    case 'D': tempCoeff1 = tempCoeff[3]; 
                              tempCoeff2 = tempCoeff[7];
                              y1_1 = tempCoeff[3];
                              y2_1 = 31000;
                              y1_2 = tempCoeff[7];
                              y2_2 = 31000; 
                              break;
                    default:
                      tempCoeff1 = tempCoeff2 = 0;
                      y1_1 = 0;
                      y2_1 = 0;
                      y1_2 = 0;
                      y2_2 = 0;
                      break;
                  }  
                 } 
               while(tempCountDurationPushButton > 0)
                {
                 --tempCountDurationPushButton;
                 frequencyOscillator(tempCoeff1, tempCoeff2, y, sizeOfFrame);
                 return;
                }
        
               while(tempCountDurationPause>0)
                {
                 --tempCountDurationPause;
                 for(int ii=0; ii<sizeOfFrame; ii++)
                  {
                   y[ii] = 0;
                  }
                 return;     
                }

               tempCountDurationPushButton = countDurationPushButton;
               tempCountDurationPause = countDurationPause;

               ++count;
               --countLengthDialButtonsArray;
              }
             readyFlag = true;
             return; 
            }

	
	/**  
             If transmitNewDialButtonsArray return 1 then the dialButtonsArray will be transmitted
             if 0, transmit is not possible and is needed to wait (nothing will be transmitted) 
             Warning! lengthDialButtonsArray must to be < NUMBER_BUTTONS and != 0, if lengthDialButtonsArray will be > NUMBER_BUTTONS
             will be transmitted only first NUMBER_BUTTONS dial buttons
             if lengthDialButtonsArray == 0 will be returned 1 and nothing will be transmitted 
             public int transmitNewDialButtonsArray(char [] dialButtonsArray, int lengthDialButtonsArray) 
         */
	public int transmitNewDialButtonsArray(char [] dialButtonsArray, int lengthDialButtonsArray)
        {
             if(!getReadyFlag()) return 0;
             if(lengthDialButtonsArray == 0)
              {
               countLengthDialButtonsArray = 0;
               count = 0;
               readyFlag = true;
               return 1;
              }
             countLengthDialButtonsArray = lengthDialButtonsArray;
             if (lengthDialButtonsArray > NUMBER_BUTTONS) countLengthDialButtonsArray = NUMBER_BUTTONS;
             for(int ii=0; ii < countLengthDialButtonsArray; ii++)
              {
               pushDialButtons[ii] = dialButtonsArray[ii];
              }

             tempCountDurationPushButton = countDurationPushButton;
             tempCountDurationPause = countDurationPause;

             count = 0;
             readyFlag = false; 
             return 1;
        }
	
        /**
            Reset generation
            public void DtmfGeneratorReset()
        */
	public void dtmfGeneratorReset()
        {
            countLengthDialButtonsArray = 0;
            count = 0;
            readyFlag = true;
        }
	
	
	//If getReadyFlag return 1 then a new button's array may be transmitted
	// if 0 transmit is not possible and is needed to wait 
	public bool getReadyFlag() 
        {
            if(readyFlag) 
                return true; 
            else 
                return false;
        }
        
        protected int mpy48sr(short o16, int o32)
        {   int    Temp0;
            int    Temp1;
            Temp0 = (((ushort)o32 * o16) + 0x4000) >> 15;
            Temp1 = (short)(o32 >> 16) * o16;
            return (int)((Temp1 << 1) + Temp0);
        }
        
        protected void frequencyOscillator(short Coeff0, short Coeff1, short [] y, int COUNT)
        {
                int Temp1_0, Temp1_1, Temp2_0, Temp2_1, Temp0, Temp1, Subject;
                short ii;
                Temp1_0 = y1_1;
                Temp1_1 = y1_2;
                Temp2_0 = y2_1;
                Temp2_1 = y2_2;
                Subject = Coeff0 * Coeff1;
                for(ii = 0; ii < COUNT; ++ii)
                {
                        Temp0 = mpy48sr(Coeff0, Temp1_0 << 1) - Temp2_0;
                        Temp1 = mpy48sr(Coeff1, Temp1_1 << 1) - Temp2_1;
                        Temp2_0 = Temp1_0;
                        Temp2_1 = Temp1_1;
                        Temp1_0 = Temp0;
                        Temp1_1 = Temp1;
                        Temp0 += Temp1;
                        if(Subject != 0)
                                Temp0 >>= 1;
                        y[ii] = (short)Temp0;
                }

                y1_1 = Temp1_0;
                y1_2 = Temp1_1;
                y2_1 = Temp2_0;
                y2_2 = Temp2_1;
        }

    public static short[] Generate(string data)
    {
        return Generate(data, 160, 130);
        //return Generate(data, 200, 160);
    }

    public static short[] Generate(string data, int durationPush, int durationPause)
    {
        int FRAME_SIZE = 160;
        short[] singleFrame = new short[FRAME_SIZE];
        Regex ex = new Regex("[^0123456789ABCD#0*]", RegexOptions.IgnoreCase);
        char[] toneSymbols = ex.Replace(data, "") .ToCharArray();
        List<short> allFrames = new List<short>();
        
        DtmfGenerator gen = new DtmfGenerator(FRAME_SIZE, durationPush, durationPause);        
        gen.transmitNewDialButtonsArray(toneSymbols, toneSymbols.Length);        
        while (!gen.getReadyFlag())
        {
            // 8 kHz, 16 bit's PCM frame's generation
            gen.dtmfGenerating(singleFrame);
            allFrames.AddRange(singleFrame);
        };

        return allFrames.ToArray();
    }
}


