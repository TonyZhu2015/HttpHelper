import urllib.request
import os

url ="http://gdl.lixian.vip.xunlei.com/download?fid=sKl/WlHqtmoPXPd87xFzTyFB6QHSHSMMAAAAAFRzusO0c6alT7l7STXUaiJURFh6&mid=666&threshold=150&tid=C48E3FF619139524CAB52894C138F03A&srcid=4&verno=1&g=5473BAC3B473A6A54FB97B4935D46A225444587A&scn=c20&i=0494A80532B5B05DDE567C61220D93406B7E22E7&t=6&ui=551334162&ti=1251309995236160&s=203627986&m=0&n=013A41973A75646F5D41638D3C6B20616E0511A930727479203201D61A313020540954C408656464690F56C40C7175616E0259812D73205B315109D42F5D205B684F03D26A5D2E6D6B1731E45F00000000&ih=0494A80532B5B05DDE567C61220D93406B7E22E7&fi=5&pi=1251309994842880&ff=0&co=0E4021B79C5A6B39E35DEBD5BA263541&cm=1&pk=lixian&ak=1:0:1:3&e=1463566907&ms=10485760&ck=25A4233F9F85C63636FF74AE7BB74914&at=EA80534ED07E66D7FC6DBDF83611B043"
cookie ="HABOWEBUID=097bd64def9c4b55412eedbfe9067907; Hm_lvt_be94a17b28798d3dc61eb511641cdd9a=1455522580; referfrom=VIP_3591; risk_tokenid=sohjc0arqqichmz71455616705653; isvip=1; usrname=; upgrade=; order=232374220; jumpkey=96633868A5EBAF7D7C795F600C73F695CEC5EA740075A7B8A93299F6F42EC9072F77A491CA1EB94DD5C8F0192B45EB4AE30027EF32FDE51918CF592B3CABFEE69ABE20E5762A556A453FE3E81FECC9C4A3226518D736E478C95A0184493EE8FF8B82ADFC121ACA5718EC76ED5765C4FE; sessionid=12C1CD35C3E08A938B909DEE51FC85F4F0D0760302C9A6D18480F51AD8770FD243ACF691E5E8F4249742E9D1A08E54C6F1A9984C73A3C94E4FE27C083D746F30; score=0; usernick=0929zhurong; logintype=0; state=0; usertype=0; usernewno=473798247; userid=551334162; shapsw=*; deviceid=wdi10.ea59e51005eca554df66915ec9d0ade1ea7b5a13d405b6315023604e0d053919; _x_a_=12C1CD35C3E08A938B909DEE51FC85F4F0D0760302C9A6D18480F51AD8770FD243ACF691E5E8F4249742E9D1A08E54C6F1A9984C73A3C94E4FE27C083D746F30; _x_t_=1_1; vip_isvip=1; vip_level=1; vip_paytype=4; user_type=1; vip_expiredate=2016-03-17; dl_enable=1; dl_size=2097153; dl_num=143; user_task_time=0.75552701950073; rw_list_open=1; queryTime=1; __xltjbr=1455616739982; _xltj=34a1455616788328b10c22a1455523359761b1c; _s34=1455823260948b1455616788328b2bhttp%3A//dynamic.cloud.vip.xunlei.com/user_task%3Fuserid%3D551334162%26st%3D4; dl_expire=365; default_version=1.0; gdriveid=25A4233F9F85C63636FF74AE7BB74914; task_nowclick=1247652398177024; lx_nf_all=page_check_all%3Dcommtask%26class_check%3D0%26page_check%3Dcommtask%26fl_page_id%3D0%26class_check_new%3D0%26set_tab_status%3D4; bg_opentime="
request=urllib.request.Request(url,headers={'Cookie':cookie})
response = urllib.request.urlopen(request)
if response.status == 200:
	filename = ""
	if response.headers.get('Content-Disposition'):
		filename = response.headers['Content-Disposition'].split('; ')[1].replace('filename=', '').strip("\"")
	total = int(response.headers['Content-Length'])
	CHUNK = 16 * 1024
	filename = os.getcwd() +"\\"+ filename
	print(filename)
	with open(filename, 'a+b') as f:
		length = os.stat(filename).st_size
		if(length != total):
			print(length)
			if length !=0:
				request=urllib.request.Request(url,headers={'Cookie':cookie , 'Range':'bytes='+str(length)+'-'})
			else:
				request=urllib.request.Request(url,headers={'Cookie':cookie})
			response = urllib.request.urlopen(request)
			print("status" + str(response.status))
			if response.status == 200 or response.status == 206:
				f.seek(0,2);
				while True:
					chunk = response.read(CHUNK)
					if not chunk: 
						break
					f.write(chunk)
		print("All bytes are downloaded.")