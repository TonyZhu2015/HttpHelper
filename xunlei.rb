require "net/http"
require "uri"

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
						puts "All bytes are downloaded"
					end
				else
					puts "File '#{filename}' found, downloading ignored."
				end
			end
		end
	rescue 
		puts "1Error downloading '{filename}'";
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
                        puts "Start downloading(#{f.size}|#{length}|#{progress}%) '#{file_name}'."
						f.seek(0, IO::SEEK_END)
						response.read_body do |segment|
							f.write(segment)
							size = File.size?(file_name).to_i
							if (progress != -1)	
								percentage = (size * 100 / length);
								if (percentage > progress)
									progress = percentage;
									puts "Downloading(#{size}|#{length}|#{progress}%) '#{file_name}'."
								end
							else
								puts "Downloading(#{size}|#{length}|--%) '#{file_name}'.";
							end
						end
						puts "Finished downloading(#{length}) '#{file_name}'."
					end
				end
			rescue
				puts "Error downloading #{file_name}. #{$!.message}}"
			ensure
				size = File.size?(file_name)
				if size != length
					interval = Random.rand(20...30)
					puts "Retry downloading '#{file_name}' after #{interval}s"
					sleep(interval)
					download2(uri, cookies, file_name, size,length)
				elsif
					File.rename(filename, filename.chomp(".download"))
					puts "All bytes are downloaded '#{file_name}'"
				end
			end
		else
			puts "Unkown status code #{code} downloading #{file_name}."
		end
	end
end

begin
	cookies ="HABOWEBUID=097bd64def9c4b55412eedbfe9067907; Hm_lvt_be94a17b28798d3dc61eb511641cdd9a=1455522580; referfrom=VIP_3591; risk_tokenid=sohjc0arqqichmz71455616705653; isvip=1; usrname=; upgrade=; order=232374220; jumpkey=96633868A5EBAF7D7C795F600C73F695CEC5EA740075A7B8A93299F6F42EC9072F77A491CA1EB94DD5C8F0192B45EB4AE30027EF32FDE51918CF592B3CABFEE69ABE20E5762A556A453FE3E81FECC9C4A3226518D736E478C95A0184493EE8FF8B82ADFC121ACA5718EC76ED5765C4FE; sessionid=12C1CD35C3E08A938B909DEE51FC85F4F0D0760302C9A6D18480F51AD8770FD243ACF691E5E8F4249742E9D1A08E54C6F1A9984C73A3C94E4FE27C083D746F30; score=0; usernick=0929zhurong; logintype=0; state=0; usertype=0; usernewno=473798247; userid=551334162; shapsw=*; deviceid=wdi10.ea59e51005eca554df66915ec9d0ade1ea7b5a13d405b6315023604e0d053919; _x_a_=12C1CD35C3E08A938B909DEE51FC85F4F0D0760302C9A6D18480F51AD8770FD243ACF691E5E8F4249742E9D1A08E54C6F1A9984C73A3C94E4FE27C083D746F30; _x_t_=1_1; vip_isvip=1; vip_level=1; vip_paytype=4; user_type=1; vip_expiredate=2016-03-17; dl_enable=1; dl_size=2097153; dl_num=143; user_task_time=0.75552701950073; rw_list_open=1; queryTime=1; __xltjbr=1455616739982; _xltj=34a1455616788328b10c22a1455523359761b1c; _s34=1455823260948b1455616788328b2bhttp%3A//dynamic.cloud.vip.xunlei.com/user_task%3Fuserid%3D551334162%26st%3D4; dl_expire=365; default_version=1.0; gdriveid=25A4233F9F85C63636FF74AE7BB74914; task_nowclick=1247652398177024; lx_nf_all=page_check_all%3Dcommtask%26class_check%3D0%26page_check%3Dcommtask%26fl_page_id%3D0%26class_check_new%3D0%26set_tab_status%3D4; bg_opentime="
	items = []
	#items << "http://gdl.lixian.vip.xunlei.com/download?fid=sKl/WlHqtmoPXPd87xFzTyFB6QHSHSMMAAAAAFRzusO0c6alT7l7STXUaiJURFh6&mid=666&threshold=150&tid=C48E3FF619139524CAB52894C138F03A&srcid=4&verno=1&g=5473BAC3B473A6A54FB97B4935D46A225444587A&scn=c20&i=0494A80532B5B05DDE567C61220D93406B7E22E7&t=6&ui=551334162&ti=1251309995236160&s=203627986&m=0&n=013A41973A75646F5D41638D3C6B20616E0511A930727479203201D61A313020540954C408656464690F56C40C7175616E0259812D73205B315109D42F5D205B684F03D26A5D2E6D6B1731E45F00000000&ih=0494A80532B5B05DDE567C61220D93406B7E22E7&fi=5&pi=1251309994842880&ff=0&co=0E4021B79C5A6B39E35DEBD5BA263541&cm=1&pk=lixian&ak=1:0:1:3&e=1463566907&ms=10485760&ck=25A4233F9F85C63636FF74AE7BB74914&at=EA80534ED07E66D7FC6DBDF83611B043"
	items << "http://gdl.lixian.vip.xunlei.com/download?fid=eYLGzwBgX2Itj5qavya+oTICIPWKtnDDAAAAADnu/K4t01UkFCidrNQGqR4Uyeg9&mid=666&threshold=150&tid=5A2EA5D8F8DE098ED7C70CA14628C504&srcid=4&verno=1&g=39EEFCAE2DD3552414289DACD406A91E14C9E83D&scn=c16&i=CAD76E2FF1FAA60E15EFC254CD1FA73832FE47FD&t=6&ui=551334162&ti=1257021073726272&s=3278943882&m=0&n=012447812D657374205301D56A203130385141C41D6C75526118119C6D363420443562C915594B2E6D0A47E45F00000000&ih=CAD76E2FF1FAA60E15EFC254CD1FA73832FE47FD&fi=17&pi=1257021072546560&ff=0&co=4F2437FBE5F0606F4761780469CAEA9B&cm=1&pk=lixian&ak=1:0:1:3&e=1463997947&ms=10485760&ck=25A4233F9F85C63636FF74AE7BB74914&at=51CCF10EC43377D3F1A7970E85BFDC6F"
	threads = items.map do |url|
		Thread.new(url) do |url|
			download(url, cookies)
		end
	end 
	threads.each {|t| t.join}
end


