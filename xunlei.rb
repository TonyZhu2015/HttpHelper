require "net/http"
require "uri"

trap("INT") do
	exit!
end

def separate(n, c = ',')
	m = n
	str = ''
	loop do
		m,r = m.divmod(1000)
		return str.insert(0,"#{r}")	if m.zero?
		str.insert(0,"#{c}#{"%03d" % r}")
	end
end

def download(url, cookies)
	filename = ""
	begin
		uri = URI.parse(url)
		http = Net::HTTP.start(uri.host)
		request = Net::HTTP::Get.new(uri.request_uri)
		request['cookie'] = cookies
		request["Range"] = "bytes=0-1"
		http.request(request) do |response|
		    code = response.code
			if code == "302"
				url = response["location"]
				download(url, cookies)
			elsif code == "200" || code == "206"
				content_disposition = response["content-disposition"]
				content_range = response["Content-Range"];
				length = content_range[content_range.index("/")+1..-1].to_i
				filename = content_disposition.match(/filename=(\"?)(.+)\1/)[2]
				if !File.file?(filename)
					filename += ".download"
					size = File.size?(filename)
					if size != length
						download2(uri, cookies, filename, size, length)
					elsif
						File.rename(filename, filename.chomp(".download"))
						log "All bytes are downloaded"
					end
				else
					log "File '#{filename}' found, downloading ignored."
				end
			end
		end
	rescue 
		log "Error downloading '#{filename}'";
		raise
	end
end

def download2(uri, cookies, file_name, offset,length)
	http = Net::HTTP.start(uri.host)
	request = Net::HTTP::Get.new(uri.request_uri)
	request['cookie'] = cookies
	if offset == nil
		offset = 0;
	elsif offset > 0
		request["Range"] = "bytes=#{offset}-"
	end

	http.request(request) do |response|
	    code = response.code
		if code == "200" || code == "206"
			begin
				open(file_name, 'ab+') do |f|
					if f.size != length
						progress = (length != 0 && length != -1) ? (offset * 100 / length) : -1;
                        log "Start downloading(#{separate(f.size)}|#{separate(length)}|#{separate(progress)}%) '#{file_name}'."
						f.seek(0, IO::SEEK_END)
						response.read_body do |segment|
							f.write(segment)
							size = File.size?(file_name).to_i
							if (progress != -1)
								percentage = (size * 100 / length);
								if (percentage > progress)
									progress = percentage;
									log "Downloading(#{separate(size)}|#{separate(length)}|#{separate(progress)}%) '#{file_name}'."
								end
							else
								log "Downloading(#{separate(size)}|#{separate(length)}|--%) '#{file_name}'.";
							end
						end
						log "Finished downloading(#{separate(length)}) '#{file_name}'."
					end
				end
			rescue 
				log "Error downloading '#{file_name}'"
				raise
			ensure
				size = File.size?(file_name)
				if size != length
					interval = Random.rand(20...30)
					log "Retry downloading '#{file_name}' after #{interval}s"
					sleep(interval)
					download2(uri, cookies, file_name, size,length)
				elsif
					File.rename(filename, filename.chomp(".download"))
					log "All bytes are downloaded '#{file_name}'"
				end
			end
		else
			log "Unkown status code #{code} downloading #{file_name}."
		end
	end
end

def log(message)
	puts "#{Time.now.strftime("%Y-%m-%d %H:%M:%S")}: #{message}"
end

begin
	cookies ="HABOWEBUID=097bd64def9c4b55412eedbfe9067907; Hm_lvt_be94a17b28798d3dc61eb511641cdd9a=1455522580; referfrom=VIP_3591; risk_tokenid=sohjc0arqqichmz71455616705653; isvip=1; usrname=; upgrade=; order=232374220; jumpkey=96633868A5EBAF7D7C795F600C73F695CEC5EA740075A7B8A93299F6F42EC9072F77A491CA1EB94DD5C8F0192B45EB4AE30027EF32FDE51918CF592B3CABFEE69ABE20E5762A556A453FE3E81FECC9C4A3226518D736E478C95A0184493EE8FF8B82ADFC121ACA5718EC76ED5765C4FE; sessionid=12C1CD35C3E08A938B909DEE51FC85F4F0D0760302C9A6D18480F51AD8770FD243ACF691E5E8F4249742E9D1A08E54C6F1A9984C73A3C94E4FE27C083D746F30; score=0; usernick=0929zhurong; logintype=0; state=0; usertype=0; usernewno=473798247; userid=551334162; shapsw=*; deviceid=wdi10.ea59e51005eca554df66915ec9d0ade1ea7b5a13d405b6315023604e0d053919; _x_a_=12C1CD35C3E08A938B909DEE51FC85F4F0D0760302C9A6D18480F51AD8770FD243ACF691E5E8F4249742E9D1A08E54C6F1A9984C73A3C94E4FE27C083D746F30; _x_t_=1_1; vip_isvip=1; vip_level=1; vip_paytype=4; user_type=1; vip_expiredate=2016-03-17; dl_enable=1; dl_size=2097153; dl_num=143; user_task_time=0.75552701950073; rw_list_open=1; queryTime=1; __xltjbr=1455616739982; _xltj=34a1455616788328b10c22a1455523359761b1c; _s34=1455823260948b1455616788328b2bhttp%3A//dynamic.cloud.vip.xunlei.com/user_task%3Fuserid%3D551334162%26st%3D4; dl_expire=365; default_version=1.0; gdriveid=25A4233F9F85C63636FF74AE7BB74914; task_nowclick=1247652398177024; lx_nf_all=page_check_all%3Dcommtask%26class_check%3D0%26page_check%3Dcommtask%26fl_page_id%3D0%26class_check_new%3D0%26set_tab_status%3D4; bg_opentime="
	items = []
	items << "http://gdl.lixian.vip.xunlei.com/download?fid=28JlbgskWaFjzV4xVXF+w727VXOw7oUPAAAAAH4d/XmwYdbsfRFchzvkzPW01Q1c&mid=666&threshold=150&tid=53C5E61419C7A53D4107694BA9962B9C&srcid=4&verno=1&g=7E1DFD79B061D6EC7D115C873BE4CCF5B4D50D5C&scn=c8&i=0494A80532B5B05DDE567C61220D93406B7E22E7&t=6&ui=551334162&ti=1251309995039552&s=260435632&m=0&n=013A41973A75646F5D41638D3C6B20616E0511A930727479203201D61A3033204114458B7F45726F740852C41E7373696D085D852B696F6E203A00D46730705D203A59CA6D36355D2E0C5A925F00000000&ih=0494A80532B5B05DDE567C61220D93406B7E22E7&fi=2&pi=1251309994842880&ff=0&co=CF0D1C3D89B85F0F5030D63282BC55B0&cm=1&pk=lixian&ak=1:0:1:3&e=1464152634&ms=10485760&ck=25A4233F9F85C63636FF74AE7BB74914&at=0A0290A5B9DB42343E4FA7082DFCE052"
	#items << "http://gdl.lixian.vip.xunlei.com/download?fid=Fo40WNvnK+X9kPgHXAf5UQJXkO/38Zg9AAAAANOiSh7oCtUQJff7W1E4IRRSABHo&mid=666&threshold=150&tid=7684B0C17A03852BC9CFCFE98405908C&srcid=4&verno=1&g=D3A24A1EE80AD51025F7FB5B51382114520011E8&scn=c24&i=45E0ADBA0DEEB46036E679D6CC347FF54519BE5A&t=6&ui=551334162&ti=1253250687966016&s=1033433591&m=0&n=012C5E8A2C7465722E23448371576172734F62812D6965732E531FD53066362E420D5E8B3B2E6F6E2E15598171466F72651245CA196C6F6F724F06D66F702E78325705CA174454565B044B90295D2E6D705531E45F00000000&ih=45E0ADBA0DEEB46036E679D6CC347FF54519BE5A&fi=0&pi=1253250687900416&ff=0&co=DD4DFBB76353856A0632CBBDD2D6D366&cm=1&pk=lixian&ak=1:0:1:3&e=1464228241&ms=10485760&ck=25A4233F9F85C63636FF74AE7BB74914&at=F15FAE415BD2035DD3D6A4D73A3A5F68"
	threads = items.map do |url|
		Thread.new(url) do |url|
			download(url, cookies)
		end
	end
	threads.each {|t| t.join}
end
