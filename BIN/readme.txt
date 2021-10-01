Это консольное приложение, которое разбирает DTMF GPS пакеты от радиостанций (пакеты координат).
К таким радиостанциям относятся Abbree AR-F8, Zastone ZT-889G и прочие. А также пакеты APRS AFSK1200.
Оно позволяет прослушивать звуковую карту и преобразовывать полученные пакеты в текстовую строку,
которая выводится в консоль, через веб-сервер, через APRS-сервер и может отправлять разобранные
данные по сети средствами HTTP-GET, TCP-APRS, UDP-APRS на внешние сервера.
Для прослушивания используются устройства аудиовхода (записи). Т.е. вы можете подключить вашу 
радиостанцию к линейному входу звуковой карты или к микрофонному входу и следить за эфиром.
Либо подключить SDR-приемник и анализировать звук с него.

Приложение может работать в режиме веб-сервера, подключившись к которому можно получать информацию
обо всех разобранных пакетах координат, а также в режиме APRS-сервера, отправляя всем клиентам
стандартные APRS-пакеты с координатами радиостанций.

Также вы можете настроить отправку пакетов на внешний HTTP или APRS сервер.

Начиная с версии 0.0.1.5 приложение поддерживает прием пакетов APRS AFSK1200.
Для этого используется параметр /afsk=.. (только в режимах /listen, либо /agw, либо /kiss)

Начиная с версии 0.0.1.15 приложение умеет работать по KISS/AGW протоколу.
Вы можете подключиться к AGW Packet Engine или напрямую через KISS к TNC
по TCP/IP или через Serial COM Port и получать APRS-пакеты из эфира.
Для этого используется параметр /age, либо /kiss

Рекомендуемые значения для настройки DTMF в радиостанции:
	DTMF Transmit time: 160 - 200 ms (200 ms наверняка)
	DTMF Interval time: 130 - 200 ms (200 ms наверняка)
	6-тизначный ANI CODE !!!

Автор: dkxce (dkxce@mail.ru) http://www.radioscanner.ru/forum/index.php?action=userinfo&user=55108

*** /// *** /// *** /// *** /// *** /// *** /// *** /// *** /// *** /// *** /// *** /// *** /// *** /// *** /// *** /// ***
*** /// *** /// *** /// *** /// *** /// *** /// *** /// *** /// *** /// *** /// *** /// *** /// *** /// *** /// *** /// ***
*** /// *** /// *** /// *** /// *** /// *** /// *** /// *** /// *** /// *** /// *** /// *** /// *** /// *** /// *** /// ***

Запуск в тестовом режиме (проврка работоспособности DTMF):

	APRSAIR.exe
	
Получение списка звуковых устройств ввода (записи DTMF/AFSK1200):

	APRSAIR.exe /listrecorddevices
	APRSAIR_List_input_devices.cmd
	
Преобразование пакета (строки) в звуковой сигнал DTMF:

	APRSAIR.exe /encode "[PACKET]" "[FILE.WAV]"
	APRSAIR.exe /encode "926801  B0B  023C#7B0  034#A030  00198000" "encoded_file.wav"
	packet_to_dtmf.cmd
	
	APRSAIR.exe /encgeo "CALLID B0B LAT.XX LON.XX ALTITUDE" "[FILE.WAV]"
	APRSAIR.exe /encgeo "926801 B0B 55.5 37.5 00198000" "encoded_file.wav"
	geo_to_dtmf.cmd
	
Преобразование звукового сигнала DTMF из файла в текст (файл):

	APRSAIR.exe /decode "[FILE.WAV]"
	APRSAIR.exe /decode "encoded_file.wav"

	APRSAIR.exe /decode "[FILE.WAV]" "[FILE.TXT]"
	APRSAIR.exe /decode "encoded_file.wav" "original_packet.txt"
	APRSAIR_to_packet.cmd

	APRSAIR.exe /decgeo "encoded_file.wav" "original_packet.txt"
	APRSAIR.exe /decgeo "encoded_file.wav" "original_packet.txt"	
	APRSAIR_to_geo.cmd
	
Преобразование APRS пакета (строки) в звуковой сигнал AFSK1200:

	APRSAIR.exe /encaprs "[PACKET]" "[FILE.WAV]"
	APRSAIR.exe /encaprs "ZADIRA>APRS,WIDE1-1,WIDE2-2:=5539.03N/03729.50EM275/029" "test_APRS_result.wav"
	APRSAIR_Encode_APRS.cmd
	
Преобразование APRS пакета (файла со списком комманд) в звуковой сигнал AFSK1200:

	APRSAIR.exe /encaprsf "[PACKET.TXT]" "[FILE.WAV]"
	APRSAIR.exe /encaprsf "TEST_APRS_multiline.txt" "TEST_APRS_multiline.wav"
	APRSAIR_Encode_APRS_Multiline.cmd
	
Преобразование звукового сигнала AFSK1200 из файла в APRS пакет (файл):

	APRSAIR.exe /decaprs "[FILE.WAV]"
	APRSAIR.exe /decaprs "test_APRS_result.wav"

	APRSAIR.exe /decode "[FILE.WAV]" "[FILE.TXT]"
	APRSAIR.exe /decode "test_APRS_result.wav" "test_APRS_result.txt"
	APRSAIR_Decode_APRS.cmd
	
Работа в режиме прослушки аудиокарты (потоковый режим):

	APRSAIR.exe /listen /source=1 [/afsk=0] [/send="..."] [/aprs="tcp://.../"] [/aprs="udp://.../"] [/httpserv=80] [/aprsserv=14580] [/nogps2console] [/useNormalPassw] [/serverName="..."]

	Параметры:
		/listen - задаем режим работы
		/source=... - с какого аудиоустройства в системе слушать аудиопоток (0,1,2...)
			          используйте /listrecorddevices для получения списка устройств 
					  (Record Devices)
		/afsk=... c какой карты в системе слушать аудиопоток (0,1,2...) 
					  для анализа APRS AFSK1200 сигнала
					  используйте /listrecorddevices для получения списка устройств 
					  (AFSK Devices)
		/send="http://..." - отправлять координаты по ссылке
			 ="http://127.0.0.1/?user={ID}&lat={LAT}&lon={LON}&alt={ALT}"
		/aprs="tcp://..." - держать коннект с APRS-IS сервером и отправлять ему координаты
			 ="tcp://username:password@servername:serverport/"
			 ="tcp://callsign:password@euro.aprs2.net:14580"
		/aprs="udp://..." - оптравлять данные на APRS-IS сервер через UDP
			 ="udp://username:password@servername:serverport/" - оптравлять данные на APRS-IS сервер через UDP
			 ="udp://callsign:password@russia.aprs2.net:8080" - оптравлять данные на APRS-IS сервер через UDP
		/httpserv=80 - веб сервер на порту 80 для вывода информации о принятых координатах (с картой)
		/aprsserv=14580 - APRS сервер на порту 14580 для вывода информации о принятых координатах
		/nogps2console - не выводится GPS информация в консоль
		/serverName="HTTP & APRS Server Name" - имя сервера
		/useNormalPassw - использовать правильный пароль APRS для отправки данных через UDP (только через UDP!)
	
		Из файла `users_replace_list.txt` берется список соответствия CALLSIGN и ANI_ID
		Из файла `\WEB\index.html` берется ответ HTTP сервера
		Из папки `\WEB` берется карта и ответ HTTP сервера

	Пример:
		APRSAIR.exe /listen /source=1 /send="http://127.0.0.1/?user={ID}&lat={LAT}&lon={LON}&alt={ALT}" /aprs="tcp://UNKNOWN:-1@russia.aprs2.net:14580/" /aprs="udp://UNKNOWN:-1@russia.aprs2.net:8080"  /httpserv=80 /aprsserv=14580 /serverName="My APRSAIR"
	
	Пример:
		APRSAIR_Run_as_WaveListener.cmd
		
Работа в режиме клиента KISS/AGW:

	APRSAIR.exe /agw /source=127.0.0.1:8000:0 [/afsk=0] [/send="..."] [/aprs="tcp://.../"] [/aprs="udp://.../"] [/httpserv=80] [/aprsserv=14580] [/nogps2console] [/useNormalPassw] [/serverName="..."]
	APRSAIR.exe /kiss /source=127.0.0.1:8100 [/afsk=0] [/send="..."] [/aprs="tcp://.../"] [/aprs="udp://.../"] [/httpserv=80] [/aprsserv=14580] [/nogps2console] [/useNormalPassw] [/serverName="..."]
	APRSAIR.exe /kiss /source=COM3:9600 [/afsk=0] [/send="..."] [/aprs="tcp://.../"] [/aprs="udp://.../"] [/httpserv=80] [/aprsserv=14580] [/nogps2console] [/useNormalPassw] [/serverName="..."]

	Параметры:
		/agw  - задаем режим работы AGW
		/kiss - задаем режим работы KISS
		/source=... - server:port:radio для AGW (например: 127.0.0.1:8000:0) где radio - порт/номер радио в AGW Packet Engine
		            - server:port для Kiss через TCP/IP (например: 127.0.0.1:8100)
					- serial:baud для Kiss через COM (например: COM3:9600)
		/afsk=... c какой карты в системе слушать аудиопоток (0,1,2...) 
					  для анализа APRS AFSK1200 сигнала
					  используйте /listrecorddevices для получения списка устройств 
					  (AFSK Devices)
		/send="http://..." - отправлять координаты по ссылке
			 ="http://127.0.0.1/?user={ID}&lat={LAT}&lon={LON}&alt={ALT}"
		/aprs="tcp://..." - держать коннект с APRS-IS сервером и отправлять ему координаты
			 ="tcp://username:password@servername:serverport/"
			 ="tcp://callsign:password@euro.aprs2.net:14580"
		/aprs="udp://..." - оптравлять данные на APRS-IS сервер через UDP
			 ="udp://username:password@servername:serverport/" - оптравлять данные на APRS-IS сервер через UDP
			 ="udp://callsign:password@russia.aprs2.net:8080" - оптравлять данные на APRS-IS сервер через UDP
		/httpserv=80 - веб сервер на порту 80 для вывода информации о принятых координатах (с картой)
		/aprsserv=14580 - APRS сервер на порту 14580 для вывода информации о принятых координатах
		/nogps2console - не выводится GPS информация в консоль
		/serverName="HTTP & APRS Server Name" - имя сервера
		/useNormalPassw - использовать правильный пароль APRS для отправки данных через UDP (только через UDP!)
	
		Из файла `users_replace_list.txt` берется список соответствия CALLSIGN и ANI_ID
		Из файла `\WEB\index.html` берется ответ HTTP сервера
		Из папки `\WEB` берется карта и ответ HTTP сервера
		
	Примеры:
		APRSAIR.exe /agw /source=127.0.0.1:8000:0 /send="http://127.0.0.1/?user={ID}&lat={LAT}&lon={LON}&alt={ALT}" /aprs="tcp://UNKNOWN:-1@russia.aprs2.net:14580/" /aprs="udp://UNKNOWN:-1@russia.aprs2.net:8080"  /httpserv=80 /aprsserv=14580 /serverName="My APRSAIR"
		
		APRSAIR.exe /kiss /source=127.0.0.1:8100 /send="http://127.0.0.1/?user={ID}&lat={LAT}&lon={LON}&alt={ALT}" /aprs="tcp://UNKNOWN:-1@russia.aprs2.net:14580/" /aprs="udp://UNKNOWN:-1@russia.aprs2.net:8080"  /httpserv=80 /aprsserv=14580 /serverName="My APRSAIR"
		
		APRSAIR.exe /kiss /source=COM3:9600 /send="http://127.0.0.1/?user={ID}&lat={LAT}&lon={LON}&alt={ALT}" /aprs="tcp://UNKNOWN:-1@russia.aprs2.net:14580/" /aprs="udp://UNKNOWN:-1@russia.aprs2.net:8080"  /httpserv=80 /aprsserv=14580 /serverName="My APRSAIR"
		
	Примеры:
		APRSAIR_Run_AGW.cmd
		APRSAIR_Run_KISS_TCP.cmd