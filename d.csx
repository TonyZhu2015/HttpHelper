#r "tcp-test.dll"
 
var folder = @"C:\Users\Rong\Pictures\";
//var folder = @"C:\Users\tony\Desktop\C#\Playground\";
var cookie = "HABOWEBUID=097bd64def9c4b55412eedbfe9067907; Hm_lvt_be94a17b28798d3dc61eb511641cdd9a=1455522580; referfrom=VIP_3591; risk_tokenid=sohjc0arqqichmz71455616705653; isvip=1; usrname=; upgrade=; order=232374220; jumpkey=96633868A5EBAF7D7C795F600C73F695CEC5EA740075A7B8A93299F6F42EC9072F77A491CA1EB94DD5C8F0192B45EB4AE30027EF32FDE51918CF592B3CABFEE69ABE20E5762A556A453FE3E81FECC9C4A3226518D736E478C95A0184493EE8FF8B82ADFC121ACA5718EC76ED5765C4FE; sessionid=12C1CD35C3E08A938B909DEE51FC85F4F0D0760302C9A6D18480F51AD8770FD243ACF691E5E8F4249742E9D1A08E54C6F1A9984C73A3C94E4FE27C083D746F30; score=0; usernick=0929zhurong; logintype=0; state=0; usertype=0; usernewno=473798247; userid=551334162; shapsw=*; deviceid=wdi10.ea59e51005eca554df66915ec9d0ade1ea7b5a13d405b6315023604e0d053919; _x_a_=12C1CD35C3E08A938B909DEE51FC85F4F0D0760302C9A6D18480F51AD8770FD243ACF691E5E8F4249742E9D1A08E54C6F1A9984C73A3C94E4FE27C083D746F30; _x_t_=1_1; vip_isvip=1; vip_level=1; vip_paytype=4; user_type=1; vip_expiredate=2016-03-17; dl_enable=1; dl_size=2097153; dl_num=143; user_task_time=0.75552701950073; rw_list_open=1; queryTime=1; __xltjbr=1455616739982; _xltj=34a1455616788328b10c22a1455523359761b1c; _s34=1455823260948b1455616788328b2bhttp%3A//dynamic.cloud.vip.xunlei.com/user_task%3Fuserid%3D551334162%26st%3D4; dl_expire=365; default_version=1.0; gdriveid=25A4233F9F85C63636FF74AE7BB74914; task_nowclick=1247652398177024; lx_nf_all=page_check_all%3Dcommtask%26class_check%3D0%26page_check%3Dcommtask%26fl_page_id%3D0%26class_check_new%3D0%26set_tab_status%3D4; bg_opentime=";
var items = new List<string>();
//items.Add("http://gdl.lixian.vip.xunlei.com/download?fid=TzNMOdTxxgT47UcfzXvwsiMGsK4kO3MVAAAAAPkbxpFVC21rOVThAeIOwXY3uO6/&mid=666&threshold=150&tid=C3CD85474C28766E06C5FBF5B0622A2D&srcid=4&verno=1&g=F91BC691550B6D6B3954E101E20EC17637B8EEBF&scn=c16&i=77A508280851E8CCA29639F5E22406061FDACCF4&t=6&ui=551334162&ti=1247652398504768&s=359873316&m=0&n=01255E9331746F6E2E2053863A792E53305774D46C2E373230111FD1713143682E235D910D61792E5204748A3C2D4465652B509D1E686D65644F5C8F2900000000&ih=77A508280851E8CCA29639F5E22406061FDACCF4&fi=5&pi=1247652398177024&ff=0&co=41C3DA8E9AA6ACD1CD03A590F967719E&cm=1&pk=lixian&ak=1:0:1:3&e=1463299884&ms=10485760&ck=25A4233F9F85C63636FF74AE7BB74914&at=1B636A1613992BB9D6EE53BE0DC55A34");
items.Add("http://gdl.lixian.vip.xunlei.com/download?fid=GEY5fDSpe7yf8Hsbdq91pIKKgNjb71f5AAAAADPjLH2FvtcXsuePiyMQs3GRwlSB&mid=666&threshold=150&tid=AB9397D75385294388F46C427AC7CD6D&srcid=4&verno=1&g=33E32C7D85BED717B2E78F8B2310B37191C25481&scn=c16&i=9B5458565AD6B5E66273F92A17009EA4D6AD1C6C&t=6&ui=551334162&ti=1249932647537472&s=4183289819&m=0&n=013559817F4D61727408508A7F323031354100D467307020572473C91B4C2078325705C41E43332D4A387ACA326B760000&ih=9B5458565AD6B5E66273F92A17009EA4D6AD1C6C&fi=1&pi=1249932647471872&ff=0&co=98CEFD00AE6DFEE6DDD29B83D860D541&cm=1&pk=lixian&ak=1:0:1:3&e=1463470394&ms=10485760&ck=25A4233F9F85C63636FF74AE7BB74914&at=C2290408BE045A87554FD7DB99F5DC8D");
items.Add("http://gdl.lixian.vip.xunlei.com/download?fid=g1ROVoMG2nUTjtZRmNgDczI/Hnc2OkIVAAAAAPARt/inX7rD8RFPJ0k3q0XmOOUD&mid=666&threshold=150&tid=FCE41BD810C4DBAF377B1B7F463977E0&srcid=4&verno=1&g=F011B7F8A75FBAC3F1114F274937AB45E638E503&scn=c16&i=77A508280851E8CCA29639F5E22406061FDACCF4&t=6&ui=551334162&ti=1247652398635840&s=356661814&m=0&n=01255E9331746F6E2E2053863A792E53305774D46B2E373230111FD1713143682E235D910D61792E5204748A3C2D4465652B509D1E686D65644F5C8F2900000000&ih=77A508280851E8CCA29639F5E22406061FDACCF4&fi=7&pi=1247652398177024&ff=0&co=31F4DF5F2761AEAB452C7030CD660079&cm=1&pk=lixian&ak=1:0:1:3&e=1463299884&ms=10485760&ck=25A4233F9F85C63636FF74AE7BB74914&at=3DE9737552AE3A3187A158E2C887E725");
//items.Add("http://gdl.lixian.vip.xunlei.com/download?fid=GwDqnk7K+CL/AW/UbQhPCiEeJtt1wpIVAAAAAAafhnKsxtjiEl+behCFMlELF3A5&mid=666&threshold=150&tid=45326759F0AFED6F6E1388C9A2CEBA59&srcid=4&verno=1&g=069F8672ACC6D8E2125F9B7A108532510B177039&scn=c16&i=77A508280851E8CCA29639F5E22406061FDACCF4&t=6&ui=551334162&ti=1247652398766912&s=361939573&m=0&n=01255E9331746F6E2E2053863A792E53305774D46A2E373230111FD1713143682E235D910D61792E5204748A3C2D4465652B509D1E686D65644F5C8F2900000000&ih=77A508280851E8CCA29639F5E22406061FDACCF4&fi=9&pi=1247652398177024&ff=0&co=7213DEBF735A1CD4E533F6C0BE56BB5F&cm=1&pk=lixian&ak=1:0:1:3&e=1463299884&ms=10485760&ck=25A4233F9F85C63636FF74AE7BB74914&at=A8E2520EC8C930CC404368BF87665894");
items.Add("http://gdl.lixian.vip.xunlei.com/download?fid=UvAl8j5f1cC6pxUfxW54LTc25fU6BYoVAAAAAJMRnRz2Ux0ucfX7ckcLX2id4wwE&mid=666&threshold=150&tid=FCADC354FDDA83F40ADF4F46F2DF02CC&srcid=4&verno=1&g=93119D1CF6531D2E71F5FB72470B5F689DE30C04&scn=c16&i=77A508280851E8CCA29639F5E22406061FDACCF4&t=6&ui=551334162&ti=1247652398897984&s=361366842&m=0&n=01255E9331746F6E2E2053863A792E53305774D4692E373230111FD1713143682E235D910D61792E5204748A3C2D4465652B509D1E686D65644F5C8F2900000000&ih=77A508280851E8CCA29639F5E22406061FDACCF4&fi=11&pi=1247652398177024&ff=0&co=B8F926B578BEF4ED967CC5AB235BBDC6&cm=1&pk=lixian&ak=1:0:1:3&e=1463299884&ms=10485760&ck=25A4233F9F85C63636FF74AE7BB74914&at=F68DC47235583158EFD02721ECC7E947");
items.Add("http://gdl.lixian.vip.xunlei.com/download?fid=QkoIw852WxmAsrwRNpgt35DZRaZqa44VAAAAACwh66cq893MEi9zBdgoSsq0/oLn&mid=666&threshold=150&tid=22D0E19B3BF966072F41E14B0540FBAF&srcid=4&verno=1&g=2C21EBA72AF3DDCC122F7305D8284ACAB4FE82E7&scn=c16&i=77A508280851E8CCA29639F5E22406061FDACCF4&t=6&ui=551334162&ti=1247652399029056&s=361655146&m=0&n=01255E9331746F6E2E2053863A792E53305774D4682E373230111FD1713143682E235D910D61792E5204748A3C2D4465652B509D1E686D65644F5C8F2900000000&ih=77A508280851E8CCA29639F5E22406061FDACCF4&fi=13&pi=1247652398177024&ff=0&co=FB2FAD3ACD3DC7DA09895B50E1B0FFA1&cm=1&pk=lixian&ak=1:0:1:3&e=1463299884&ms=10485760&ck=25A4233F9F85C63636FF74AE7BB74914&at=4E58201DE36FFEDAF9FFFB372ECF223E");
items.Add("http://gdl.lixian.vip.xunlei.com/download?fid=QlM4mW5jdo696WKZ7lvFzzf3YLkD+uAgAAAAAElPSrPE/6m2pukzRxTbWRZFx/QN&mid=666&threshold=150&tid=90102936BA7093D267DB2EEB3B106BE5&srcid=4&verno=1&g=494F4AB3C4FFA9B6A6E9334714DB591645C7F40D&scn=c16&i=77A508280851E8CCA29639F5E22406061FDACCF4&t=6&ui=551334162&ti=1247652399160128&s=551614979&m=0&n=01255E9331746F6E2E2053863A792E53305774D4672E373230111FD1713143682E235D910D61792E5204748A3C2D4465652B509D1E686D65644F5C8F2900000000&ih=77A508280851E8CCA29639F5E22406061FDACCF4&fi=15&pi=1247652398177024&ff=0&co=B3B573539AE624D6E519C9ACA3651572&cm=1&pk=lixian&ak=1:0:1:3&e=1463299884&ms=10485760&ck=25A4233F9F85C63636FF74AE7BB74914&at=D74C34EBC80D01DFD467A24659B6C21F");
new Sulfate().Download(cookie, items, folder);

Console.WriteLine("Exit");