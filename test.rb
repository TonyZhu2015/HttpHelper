trap("INT") do
	puts "exit"
	exit!
end


def run()
	loop do
		begin
			print "a"
			sleep 2
		ensure
			run()
		end 
	end
end

run()