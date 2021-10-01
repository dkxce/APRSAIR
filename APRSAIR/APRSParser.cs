using System;
using System.Collections.Generic;

namespace APRSForwarder
{
    public class APRSParser
    {
        public string Callsign;
        public string PacketType;
        public string Latitude;
        public string Longitude;
        public string Altitude;
        public string GPSTime;
        public string RawData;
        public string Symbol;
        public string Heading;
        public string PHG;
        public string Speed;
        public string Destination;
        public string Status;
        public string WindDirection;
        public string WindSpeed;
        public string WindGust;
        public string WeatherTemp;
        public string RainHour;
        public string RainDay;
        public string RainMidnight;
        public string Humidity;
        public string Pressure;
        public string Luminosity;
        public string Snowfall;
        public string Raincounter;
        public string Error;

        private System.Globalization.CultureInfo ci = System.Globalization.CultureInfo.InstalledUICulture;
        private System.Globalization.NumberFormatInfo ni;
        public System.Globalization.NumberFormatInfo DotDelimiter { get { return ni; } }

        public APRSParser()
        {
            ni = (System.Globalization.NumberFormatInfo) ci.NumberFormat.Clone();
            ni.NumberDecimalSeparator = ".";

         Callsign = "Unknown";
         PacketType = "Unknown";
         Latitude = "";
         Longitude = "";
         Altitude = "0";
         GPSTime = "";
         RawData = "";
         Symbol = "";
         Heading = "000";
         PHG = "";
         Speed = "";
         Destination = "";
         Status = "";
         WindDirection = "";
         WindSpeed = "";
         WindGust = "";
         WeatherTemp = "";
         RainHour = "";
         RainDay = "";
         RainMidnight = "";
         Humidity = "";
         Pressure = "";
         Luminosity = "";
         Snowfall = "";
         Raincounter = "";

         Error = "";
        }
        
        public void Parse(string line)
        {
            try
            {
              /*  // Strip CR then LF at the EOL
                if (line.Substring(line.Length - 1, 1) == "\r" || line.Substring(line.Length - 1, 1) == "\n")
                {
                    line = line.Remove(0, (line.Length - 2));
                }
               */ 
               
                int SecondChr;
                int ThirdChr;
                int FourthChr;

                int LongOffset = 0;
                // Is this a valid APRS packet?
                int FirstChr = line.IndexOf(">");
                if (FirstChr != -1)
                {
                    // Parse Callsign
                    Callsign = line.Remove(FirstChr, (line.Length - FirstChr));
                    Destination = line.Substring(FirstChr + 1, 6);

                    // Is this a status report?
                    FirstChr = line.IndexOf(":>");
                    if (FirstChr > line.IndexOf(">"))
                    {
                        PacketType = "Status Report";
                        char[] RawArray = line.Substring(FirstChr + 2, (line.Length - FirstChr - 2)).ToCharArray();
                        if (Convert.ToString(RawArray[6]).ToLower() == "z")
                        {
                            GPSTime = line.Substring(FirstChr + 2, 6);
                            Status = line.Substring(FirstChr + 9, (line.Length - FirstChr - 9));
                          } else { 
                            Status = line.Substring(FirstChr + 2, (line.Length - FirstChr - 2));
                        }
                    }

                    // Is this a GPGGA packet?
                    FirstChr = line.IndexOf(":$GPGGA,");

                    if (FirstChr != -1)
                    {
                        PacketType = "GPGGA";
                        RawData = line.Substring(FirstChr, (line.Length - FirstChr));
                        string[] Split = RawData.Split(new Char[] { ',' });
                        GPSTime = Convert.ToString(Split[1]);

                        // Latitude
                        string degLatitude = Convert.ToString(Split[2]);
                        double degLatMin = Convert.ToDouble(degLatitude.Substring(2, degLatitude.Length - 2), ni);
                        degLatMin = (degLatMin / 60);
                        Latitude = degLatitude.Substring(0, 2) + Convert.ToString(degLatMin).Substring(1, Convert.ToString(degLatMin).Length - 1);

                        if (Convert.ToString(Split[3]) == "S")
                        {
                            Latitude = "-" + Latitude;
                        }

                        // Longitude
                        string degLongitude = Convert.ToString(Split[4]);
                        double degLonMin = Convert.ToDouble(degLongitude.Substring(3, degLongitude.Length - 3), ni);
                        degLonMin = (degLonMin / 60);
                        Longitude = degLongitude.Substring(0, 3) + Convert.ToString(degLonMin).Substring(1, Convert.ToString(degLonMin).Length - 1);

                        if (Convert.ToString(Split[5]) == "W")
                        {
                            Longitude = "-" + Longitude;
                        }

                        Altitude = Convert.ToString(Convert.ToDouble(Split[9],ni));
                    }

                    // Is this a Mic-E packet?
                    FirstChr = line.IndexOf(":`");
                    SecondChr = line.IndexOf(":'");
                    if ((FirstChr != -1) || (SecondChr != -1))
                    {
                        if (FirstChr != -1)
                        {
                            PacketType = "New Mic-E"; 
                        } else {
                            PacketType = "Old Mic-E";
                            FirstChr = SecondChr;
                        }
                       
                        char [] DestinationArray = Destination.ToCharArray();
                        // Lattitude
                        string degLatitude = Convert.ToString(Convert.ToInt16(DestinationArray[0]) & 0x0F) + Convert.ToString(Convert.ToInt16(DestinationArray[1]) & 0x0F);
                        double degLatMin = Convert.ToDouble(Convert.ToString(Convert.ToInt16(DestinationArray[2]) & 0x0F) + Convert.ToString(Convert.ToInt16(DestinationArray[3]) & 0x0F) + "." + Convert.ToString(Convert.ToInt16(DestinationArray[4]) & 0x0F) + Convert.ToString(Convert.ToInt16(DestinationArray[5]) & 0x0F), ni);
                        degLatMin = (degLatMin / 60);
                        Latitude = degLatitude + Convert.ToString(degLatMin).Substring(1, Convert.ToString(degLatMin).Length - 1);
                        if (Convert.ToInt16(DestinationArray[3]) < 80)
                        {
                            Latitude = "-" + Latitude;
                        }

                        // Longitude
                        char[] InformationField = line.Substring(FirstChr + 1, line.Length - FirstChr - 1).ToCharArray();
                        
                        if (Convert.ToInt16(DestinationArray[4]) > 79)
                        {
                           LongOffset = 100;
                        }
                        int degLongitude = Convert.ToInt16(InformationField[1]) - 28 + LongOffset;
                        if ((degLongitude > 179) && (degLongitude < 188))
                        {
                            degLongitude = degLongitude - 80;
                        }
                        if ((degLongitude > 190) && (degLongitude < 199))
                        {
                            degLongitude = degLongitude - 190;
                        }
                        double degLonMin = Convert.ToInt16(InformationField[2]) - 28;
                        if (degLonMin > 59)
                        {
                            degLonMin = degLonMin - 60;
                        }
                        degLonMin = Convert.ToDouble(Convert.ToString(degLonMin) + "." + Convert.ToString(Convert.ToInt16(InformationField[3]) - 28), ni);
                        degLonMin = (degLonMin / 60);
                        Longitude = Convert.ToString(degLongitude) + Convert.ToString(degLonMin).Substring(1, Convert.ToString(degLonMin).Length - 1);
                        if (Convert.ToInt16(DestinationArray[5]) > 79)
                        {
                            Longitude = "-" + Longitude;
                        }
                        Symbol = Convert.ToString(InformationField[8]) + Convert.ToString(InformationField[7]);

                        Speed = Convert.ToString(((Convert.ToDouble((Convert.ToInt16(InformationField[4])) - 28, ni) * 10) + (Math.Floor((Convert.ToDouble(Convert.ToInt16(InformationField[5]), ni) -28)/10))) % 800);
                        Heading = Convert.ToString((((((Convert.ToDouble(Convert.ToInt16(InformationField[5]), ni) - 28) / 10) - Math.Floor((Convert.ToDouble(Convert.ToInt16(InformationField[5]),ni) - 28) / 10)) * 1000) + (Convert.ToDouble(Convert.ToInt16(InformationField[6]),ni) - 28 )) % 400);
                        if (Convert.ToString(InformationField[13]) == "}")
                        {
                            Altitude = Convert.ToString((((Convert.ToInt32(InformationField[10]) - 33) * 8281) +((Convert.ToInt32(InformationField[11]) - 33) * 91) + ((Convert.ToInt32(InformationField[12]) - 33)) - (10000)));
                        }
                    
                    }

                    // Is this a location packet?
                    FirstChr = line.IndexOf(":/");  // With Timestamp
                        SecondChr = line.IndexOf(":!"); // Without Timestamp
                    ThirdChr = line.IndexOf(":@");  // With Timestamp and APRS Messaging
                    FourthChr = line.IndexOf(":="); // Without Timestamp and Messaging

                    if (ThirdChr != -1)
                    {
                        FirstChr = ThirdChr;
                    }

                    if (FourthChr != -1)
                    {
                        SecondChr = FourthChr;
                    }

                    if ((FirstChr != -1 && line.Substring((FirstChr + 8), 1).ToUpper() == "H") ||
                        (FirstChr != -1 && line.Substring((FirstChr + 8), 1).ToUpper() == "Z") ||
                        (FirstChr != -1 && line.Substring((FirstChr + 8), 1).ToUpper() == "/") ||
                        (SecondChr != -1 && line.Substring((SecondChr + 9), 1).ToUpper() == "S") ||
                        (SecondChr != -1 && line.Substring((SecondChr + 9), 1).ToUpper() == "N"))
                    {


                        PacketType = "Location";

                        if ((FirstChr != -1 && line.Substring((FirstChr + 8), 1).ToUpper() == "H") ||
                            (FirstChr != -1 && line.Substring((FirstChr + 8), 1).ToUpper() == "Z") ||
                            (FirstChr != -1 && line.Substring((FirstChr + 8), 1).ToUpper() == "/"))
                        {
                            GPSTime = line.Substring((FirstChr + 2), 6);
                        }
                        else
                        {
                            FirstChr = SecondChr - 7;
                        }
                        Symbol = line.Substring(FirstChr + 17, 1) + line.Substring(FirstChr + 27, 1);
                        double degLatMin = Convert.ToDouble(line.Substring(FirstChr + 11, 5),ni);
                        degLatMin = (degLatMin / 60);
                        Latitude = line.Substring(FirstChr + 9, 2) + Convert.ToString(degLatMin).Substring(1, Convert.ToString(degLatMin).Length - 1);
                        if (line.Substring(FirstChr + 16, 1) == "S")
                        {
                            Latitude = "-" + Latitude;
                        }

                        double degLonMin = Convert.ToDouble(line.Substring(FirstChr + 21, 5),ni);
                        degLonMin = (degLonMin / 60);
                        Longitude = line.Substring(FirstChr + 19, 2) + Convert.ToString(degLonMin).Substring(1, Convert.ToString(degLonMin).Length - 1);
                        if (line.Substring(FirstChr + 26, 1) == "W")
                        {
                            Longitude = "-" + Longitude;
                        }
                        // Is this packet a Weather Report?
                        if (line.Substring(FirstChr + 27, 1) == "_" && line.Substring(FirstChr + 31,1) == "/")
                        {
                            PacketType = "Weather Report";
                            WindDirection = line.Substring(FirstChr + 28, 3);
                            WindSpeed = line.Substring(FirstChr + 32, 3);
                            int[] wrpos = new int[10];
                            wrpos[0] = line.IndexOf("g", FirstChr + 27);
                            wrpos[1] = line.IndexOf("t", FirstChr + 27);
                            wrpos[2] = line.IndexOf("r", FirstChr + 27);
                            wrpos[3] = line.IndexOf("p", FirstChr + 27);
                            wrpos[4] = line.IndexOf("P", FirstChr + 27);
                            wrpos[5] = line.IndexOf("h", FirstChr + 27);
                            wrpos[6] = line.IndexOf("b", FirstChr + 27);
                            wrpos[7] = line.IndexOf("L", FirstChr + 27);
                            wrpos[8] = line.IndexOf("s", FirstChr + 27);
                            wrpos[9] = line.IndexOf("#", FirstChr + 27);

                            if (wrpos[0] != -1)
                            {
                                WindGust = line.Substring(wrpos[0] + 1, 3);
                            }
                            if (wrpos[1] != -1)
                            {
                            WeatherTemp = line.Substring(wrpos[1] + 1, 3);
                            }
                            if (wrpos[2] != -1)
                            {
                            RainHour = line.Substring(wrpos[2] + 1, 3);
                            }
                            if (wrpos[3] != -1)
                            {
                            RainDay = line.Substring(wrpos[3] + 1, 3);
                            }
                            if (wrpos[4] != -1)
                            {
                            RainMidnight = line.Substring(wrpos[4] + 1, 3);
                            }
                            if (wrpos[5] != -1)
                            {
                            Humidity = line.Substring(wrpos[5] + 1, 2);
                            }
                            if (wrpos[6] != -1)
                            {
                            Pressure =  line.Substring(wrpos[6] + 1, 3);
                            }
                            if (wrpos[7] != -1)
                            {
                            Luminosity = line.Substring(wrpos[7] + 1, 3);
                            }
                            if (wrpos[8] != -1)
                            {
                            Snowfall =  line.Substring(wrpos[8] + 1, 3);
                            }
                            if (wrpos[9] != -1)
                            {
                            Raincounter = line.Substring(wrpos[9] + 1, 3);
                            }


                        } else {
                            if (line.IndexOf("/A=") != -1)
                            {
                                Altitude = Convert.ToString(Convert.ToInt32(Convert.ToDouble(line.Substring((line.IndexOf("/A=") + 3), 6)) * 12 / 39.37));
                            }
                            if (line.Substring(FirstChr + 28, 3).ToUpper() == "PHG")
                            {
                                PHG = line.Substring(FirstChr + 31, 4);
                            }
                            if (line.Substring(FirstChr + 31, 1) == "/")
                            {
                                Heading = line.Substring(FirstChr + 28, 3);
                                Speed = line.Substring(FirstChr + 32, 3);
                            }
                        }
                    }
                    // Is this packet a Positionless Weather Report Without Position?
                    FirstChr = line.IndexOf(":_");
                    if (FirstChr != -1)
                    {
                        PacketType = "Weather Report";
                        int[] wrpos = new int[12];
                        wrpos[0] = line.IndexOf("g", FirstChr);
                        wrpos[1] = line.IndexOf("t", FirstChr);
                        wrpos[2] = line.IndexOf("r", FirstChr);
                        wrpos[3] = line.IndexOf("p", FirstChr);
                        wrpos[4] = line.IndexOf("P", FirstChr);
                        wrpos[5] = line.IndexOf("h", FirstChr);
                        wrpos[6] = line.IndexOf("b", FirstChr);
                        wrpos[7] = line.IndexOf("L", FirstChr);
                        wrpos[8] = line.IndexOf("s", FirstChr);
                        wrpos[9] = line.IndexOf("#", FirstChr);
                        wrpos[10] = line.IndexOf("c", FirstChr);
                        wrpos[11] = line.IndexOf("s", FirstChr);

                        if (wrpos[0] != -1)
                        {
                            WindGust = line.Substring(wrpos[0] + 1, 3);
                        }
                        if (wrpos[1] != -1)
                        {
                            WeatherTemp = line.Substring(wrpos[1] + 1, 3);
                        }
                        if (wrpos[2] != -1)
                        {
                            RainHour = line.Substring(wrpos[2] + 1, 3);
                        }
                        if (wrpos[3] != -1)
                        {
                            RainDay = line.Substring(wrpos[3] + 1, 3);
                        }
                        if (wrpos[4] != -1)
                        {
                            RainMidnight = line.Substring(wrpos[4] + 1, 3);
                        }
                        if (wrpos[5] != -1)
                        {
                            Humidity = line.Substring(wrpos[5] + 1, 2);
                        }
                        if (wrpos[6] != -1)
                        {
                            Pressure = line.Substring(wrpos[6] + 1, 3);
                        }
                        if (wrpos[7] != -1)
                        {
                            Luminosity = line.Substring(wrpos[7] + 1, 3);
                        }
                        if (wrpos[8] != -1)
                        {
                            Snowfall = line.Substring(wrpos[8] + 1, 3);
                        }
                        if (wrpos[9] != -1)
                        {
                            Raincounter = line.Substring(wrpos[9] + 1, 3);
                        }
                        if (wrpos[10] != -1)
                        {
                            WindDirection = line.Substring(wrpos[10] + 1, 3);
                        }
                        if (wrpos[11] != -1)
                        {
                            WindSpeed = line.Substring(wrpos[11] + 1, 3);
                        }
                        GPSTime = line.Substring(FirstChr + 2, 8);
                    }
                }
            }
            catch (Exception Err)
            {
                Error = Err.Message + Err.StackTrace;
                PacketType = "Parse Error (" + Error + ")";
            }
        }

        public static int Hash(string name)
        {
            string upname = name == null ? "" : name;
            int stophere = upname.IndexOf("-");
            if (stophere > 0) upname = upname.Substring(0, stophere);
            while (upname.Length < 9) upname += " ";

            int hash = 0x2017;
            int i = 0;
            while (i < 9)
            {
                hash ^= (int)(upname.Substring(i, 1))[0] << 16;
                hash ^= (int)(upname.Substring(i + 1, 1))[0] << 8;
                hash ^= (int)(upname.Substring(i + 2, 1))[0];
                i += 3;
            };
            return hash & 0x7FFFFF;
        }
    }
}