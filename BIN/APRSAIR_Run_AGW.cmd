@rem
@rem USAGE:
@rem   /listen - listen wave stream mode
@rem   /source=... - wave signal source 0,1,2... (Record Device)
@rem   /afsk=... - Listen Sound device for APRS AFSK1200 (AFSK Device)
@rem   /send="http://..." - send data as HTTP Get Request
@rem   /aprs="tcp://..." - connect with TCP/IP to APRS-IS Server and send data to it
@rem   /aprs="udp://..." - send data to APRS-IS via UDP
@rem   /httpserv=80 - HTTP Server for listing incoming data
@rem   /aprsserv=14580 - APRS-IS Server from listing incoming data
@rem   /nogps2console - no write gps data to console
@rem   /serverName="NoName" - set Server Name
@rem   /useNormalPassw - use normal callsign hamateur aprs password to send UDP data (only UDP!)
@rem 
@rem APRS Examples:
@rem   tcp://callsign:password@euro.aprs2.net:14580
@rem   udp://callsign:password@russia.aprs2.net:8080
@rem 
@rem 
@APRSAIR.exe /agw /source=127.0.0.1:8000:0 /!send="http://127.0.0.1/?user={ID}&lat={LAT}&lon={LON}&alt={ALT}" /!aprs="tcp://CTAKAH:12469@127.0.0.1:12015/" /!aprs="udp://UNKNOWN:-1@russia.aprs2.net:8080/" /httpserv=80 /aprsserv=14580 /serverName="My APRSAIR"