using System;
using System.IO;

class DtmfDetector
{
    public const int NUMBER_OF_BUTTONS = 65;
    private char [] pDialButtons = new char[NUMBER_OF_BUTTONS];    
    private short indexForDialButtons = 0;    
        
    protected const int COEFF_NUMBER = 18;
    protected static short [] CONSTANTS = {27860, 26745, 25529, 24216, 19747, 16384, 12773, 8967, 21319, 29769, 32706, 32210, 31778, 31226, -1009, -12772, -22811, -30555};   
    protected short [] pArraySamples;
    protected int [] T = new int[COEFF_NUMBER];
    protected short [] internalArray;
    protected int frameSize;
    protected int SAMPLES = 102;
    protected int frame_count;
    protected char prevDialButton;
    protected bool permissionFlag;

    protected int powerThreshold = 328;
    protected int dialTonesToOhersTones = 16;
    protected int dialTonesToOhersDialTones = 6;

    short norm_l(int L_var1)
    {
        short var_out;

        if (L_var1 == 0)
        {
            var_out = 0;
        }
        else
        {
            if (L_var1 == -1)
            {
                var_out = 31;
            }
            else
            {
                if (L_var1 < 0)
                {
                    L_var1 = ~L_var1;
                }

                for(var_out = 0; L_var1 < 0x40000000; var_out++)
                {
                    L_var1 <<= 1;
                }
            }
       }
        return var_out;
    }

    
    protected char dtmfDetection(short [] short_array_samples)
    {                    
        int Dial=32, Sum;
        char return_value=' ';
        int ii;
        Sum = 0;

        for(ii = 0; ii < SAMPLES; ii ++)
        {
            if(short_array_samples[ii] >= 0)
                Sum += short_array_samples[ii];
            else
                Sum -= short_array_samples[ii];
        }
        Sum /= SAMPLES;
        if(Sum < powerThreshold) 
                         return ' '; 
        
        for(ii = 0; ii < SAMPLES; ii++)
        {
            T[0] = (int)(short_array_samples[ii]);
            if(T[0] != 0)
            {
                if(Dial > norm_l(T[0]))
                {
                    Dial = norm_l(T[0]);
                }
            }
        }

        Dial -= 16;

        for(ii = 0; ii < SAMPLES; ii++)
        {
            T[0] = short_array_samples[ii];
            internalArray[ii] = (short)(T[0] << Dial);
        }
        
        goertzelFilter(CONSTANTS[0], CONSTANTS[1], internalArray, T, SAMPLES, 0); 
        goertzelFilter(CONSTANTS[2], CONSTANTS[3], internalArray, T, SAMPLES, 2); 
        goertzelFilter(CONSTANTS[4], CONSTANTS[5], internalArray, T, SAMPLES, 4); 
        goertzelFilter(CONSTANTS[6], CONSTANTS[7], internalArray, T, SAMPLES, 6); 
        goertzelFilter(CONSTANTS[8], CONSTANTS[9], internalArray, T, SAMPLES, 8); 
        goertzelFilter(CONSTANTS[10], CONSTANTS[11], internalArray, T, SAMPLES, 10); 
        goertzelFilter(CONSTANTS[12], CONSTANTS[13], internalArray, T, SAMPLES, 12); 
        goertzelFilter(CONSTANTS[14], CONSTANTS[15], internalArray, T, SAMPLES, 14); 
        goertzelFilter(CONSTANTS[16], CONSTANTS[17], internalArray, T, SAMPLES, 16); 
       
        int Row = 0;
        int Temp = 0;
        
        for(ii = 0; ii < 4; ii++)
        {
            if(Temp < T[ii]) 
            {
                Row = ii;
                Temp = T[ii];       
            }
        }

        int Column = 4;
        Temp = 0;
        
        for(ii = 4; ii < 8; ii++)
        {
            if(Temp < T[ii])
            {
                Column = ii;
                Temp = T[ii];
            }
        }

        Sum=0;
        
        for(ii = 0; ii < 10; ii++)
        {
            Sum += T[ii];
        }     
        Sum -= T[Row];
        Sum -= T[Column];
        Sum >>= 3;
        
        if(Sum == 0)
        {
            Sum = 1;
        }
        
        if(T[Row]/Sum < dialTonesToOhersDialTones) 
                                             return ' ';
        if(T[Column]/Sum < dialTonesToOhersDialTones) 
                                             return ' ';

        
        if(T[Row] < (T[Column] >> 2)) return ' ';
        
        if(T[Column] < ((T[Row] >> 1) - (T[Row] >> 3))) return ' '; 
               
        for(ii = 0; ii < COEFF_NUMBER; ii++)
            if(T[ii] == 0)
                T[ii] = 1;
        
        for(ii = 10; ii < COEFF_NUMBER; ii++)
        {            
            if(T[Row]/T[ii] < dialTonesToOhersTones) 
                                     return ' ';
            if(T[Column]/T[ii] < dialTonesToOhersTones) 
                                     return ' ';
        }

        for(ii = 0; ii < 10; ii ++)
        {
            if(T[ii] != T[Column])
            {
                if(T[ii] != T[Row])
                {
                    if(T[Row]/T[ii] < dialTonesToOhersDialTones) 
                                                   return ' ';
                    if(Column != 4)
                    {
                        if(T[Column]/T[ii] < dialTonesToOhersDialTones) 
                                                   return ' ';
                    }
                    else
                    {
                        if(T[Column]/T[ii] < (dialTonesToOhersDialTones/3)) 
                                                   return ' ';
                    }
                }
            }
        }
        
        switch (Row)
        {
            case 0: switch (Column){
                                case 4: return_value='1'; break; 
                                case 5: return_value='2'; break; 
                                case 6: return_value='3'; break; 
                                case 7: return_value='A'; break;}; 
                                break;
            case 1: switch (Column){
                                case 4: return_value='4'; break; 
                                case 5: return_value='5'; break; 
                                case 6: return_value='6'; break; 
                                case 7: return_value='B'; break;}; 
                                break;
            case 2: switch (Column){
                                case 4: return_value='7'; break; 
                                case 5: return_value='8'; break; 
                                case 6: return_value='9'; break; 
                                case 7: return_value='C'; break;}; 
                                break;
            case 3: switch (Column){
                                case 4: return_value='*'; break; 
                                case 5: return_value='0'; break; 
                                case 6: return_value='#'; break; 
                                case 7: return_value='D'; break;}
                                break;
        }

        return return_value;
    }

    /** Returning of a amount of the striked dial buttons
        public int getIndexDialButtons()
    */
    public int getIndexDialButtons()
	{return indexForDialButtons;}
    
    /** Returning of an array of the striked dial buttons     
        public char [] getDialButtonsArray()
    */
    public char [] getDialButtonsArray()
	{return pDialButtons;}
    
    /** Get ready for next array of the striked dial buttons
        public void zerosIndexDialButtons()
        call this function, when previous array of the striked dial buttons is 
        completely processed and you are ready to receiving the 
        next array        
    */
    public void zerosIndexDialButtons()
	{indexForDialButtons = 0;}


    /**     
            frameSize_ - The frame's size of the 8 kHz input samples
            public DtmfDetector(int frameSize_)
    */
    public DtmfDetector(int frameSize_)
    {
         frameSize = frameSize_;
         pDialButtons[0] = '\0';
         pArraySamples = new short [frameSize + SAMPLES];
         internalArray = new short [SAMPLES];         
         frame_count = 0;
         prevDialButton = ' ';
         permissionFlag = false;         
    }

    // The DTMF detection.
    // The size of a input_frame must be equal of a frameSize, who 
    // was set in constructor.
    public void dtmfDetecting(short [] input_frame)
    {int ii;
     char temp_dial_button;

       for(ii=0; ii < frameSize; ii++)
       {
            pArraySamples[ii + frame_count] = input_frame[ii];
       }

       frame_count += frameSize;
       int temp_index = 0;
       if(frame_count >= SAMPLES)
        { 
         while(frame_count >= SAMPLES)
          {
            if(temp_index == 0)
            {
                temp_dial_button = dtmfDetection(pArraySamples);
            }
            else
            {
                short [] tempArray = new short[pArraySamples.Length - temp_index];                
                for(int inc = 0; inc < pArraySamples.Length - temp_index; ++inc)
                {
                    tempArray[inc] = pArraySamples[temp_index + inc];
                }
                temp_dial_button = dtmfDetection(tempArray);
            }

            if(permissionFlag)
             {
              if(temp_dial_button != ' ')
               {
                pDialButtons[indexForDialButtons++] = temp_dial_button;
                pDialButtons[indexForDialButtons] = '\0';
                if(indexForDialButtons >= 64)
                             indexForDialButtons = 0;
               }
              permissionFlag = false;
             }

            if((temp_dial_button != ' ') && (prevDialButton == ' '))
             {
              permissionFlag = true;
             }

            prevDialButton = temp_dial_button;

            temp_index += SAMPLES;
            frame_count -= SAMPLES;
          }

         for(ii=0; ii < frame_count; ii++)
          {
           pArraySamples[ii] = pArraySamples[ii + temp_index];
          }        
        }

    }


    protected int mpy48sr(short o16, int o32)
    {   int    Temp0;
        int    Temp1;
	Temp0 = (((ushort)o32 * o16) + 0x4000) >> 15;
	Temp1 = (short)(o32 >> 16) * o16;
	return (int)((Temp1 << 1) + Temp0);
    }

    protected void goertzelFilter(short Koeff0, short Koeff1, short [] arraySamples, int [] Magnitude, int COUNT, int index)
    {int Temp0, Temp1;
     short ii;
     int Vk1_0 = 0, Vk2_0 = 0, Vk1_1 = 0, Vk2_1 = 0;

            for(ii = 0; ii < COUNT; ++ii)
            {                    
                    Temp0 = mpy48sr(Koeff0, Vk1_0 << 1) - Vk2_0 + arraySamples[ii];
                    Temp1 = mpy48sr(Koeff1, Vk1_1 << 1) - Vk2_1 + arraySamples[ii];
                    
                    Vk2_0 = Vk1_0;
                    Vk2_1 = Vk1_1;
                    Vk1_0 = Temp0;
                    Vk1_1 = Temp1;                    
            }

            Vk1_0 >>= 10;
            Vk1_1 >>= 10;
            Vk2_0 >>= 10;
            Vk2_1 >>= 10;
            Temp0 = mpy48sr(Koeff0, Vk1_0 << 1);
            Temp1 = mpy48sr(Koeff1, Vk1_1 << 1);
            Temp0 = (short)Temp0 * (short)Vk2_0;
            Temp1 = (short)Temp1 * (short)Vk2_1;
            Temp0 = (short)Vk1_0 * (short)Vk1_0 + (short)Vk2_0 * (short)Vk2_0 - Temp0;
            Temp1 = (short)Vk1_1 * (short)Vk1_1 + (short)Vk2_1 * (short)Vk2_1 - Temp1;
            Magnitude[index] = Temp0;
            Magnitude[index + 1] = Temp1;
            return;
    }
    
    public static string Decode(short[] dataFrames)
    {
        int FRAME_SIZE = 160;
        string toneSymbols = "";        
        short[] singleFrame = new short[FRAME_SIZE];
        int fc = 0;

        DtmfDetector det = new DtmfDetector(FRAME_SIZE);        
        while (fc < dataFrames.Length)
        {
            Array.Copy(dataFrames, fc, singleFrame, 0, FRAME_SIZE);
            fc += FRAME_SIZE;

            // 8 kHz, 16 bit's PCM frame's detection
            det.dtmfDetecting(singleFrame);
        
            if (det.getIndexDialButtons() > 0)
            {
                char[] buttons = det.getDialButtonsArray();
                for (int i = 0; i < det.getIndexDialButtons(); ++i) toneSymbols += buttons[i];
                det.zerosIndexDialButtons();
            };
        };        
        return toneSymbols;
    }
}		
