#include <iostream>
#include <fstream>
#include <string>
#include <thread>
#include <list>
#include <queue>
#include <mutex>
#include <condition_variable>

#include <winsock2.h>
#pragma comment(lib, "Ws2_32.lib")

class work_item
{
public:
	int socket;
	char buffer[256];
};

template <typename T> class blocking_queue
{
public:

	T pop()
	{
		T item = NULL;
		std::unique_lock<std::mutex> mlock(mutex);
		while (queue.empty() && !completed)
		{
			condition_variable.wait(mlock);
		}

		mtx.lock();
		if (!queue.empty())
		{
			item = queue.front();
			queue.pop();
		}
		
		mtx.unlock();
		return item;
	}

	/*void pop(T& item)
	{
		std::unique_lock<std::mutex> mlock(mutex_);
		while (queue_.empty())
		{
			cond_.wait(mlock);
		}

		item = queue_.front();
		queue_.pop();
	}*/

	void push(T& item)
	{
		std::unique_lock<std::mutex> mlock(mutex);
		queue.push(item);
		mlock.unlock();
		condition_variable.notify_one();
	}

	/*void push(T&& item)
	{
		std::unique_lock<std::mutex> mlock(mutex_);
		queue_.push(std::move(item));
		mlock.unlock();
		cond_.notify_one();
	}*/

	void finalise()
	{
		completed = true;
		condition_variable.notify_all();
	}

private:
	std::queue<T> queue;
	std::mutex mutex;
	std::mutex mtx;
	std::condition_variable condition_variable;
	bool completed;
};

const char delimiter[] = {'\r', '\n','\r','\n' };
const int size = 1024;

void handle_requests(int client_socket, blocking_queue<work_item*>* processing_queue)
{
	int  count;
	char buffer[size];
	std::list<char> byteBag;
	do
	{
		memset(buffer, 0, size);
		count = recv(client_socket, buffer, size, 0);
		const char* i = delimiter;
		int i2 = 10;
		//l->socket = client_socket;
		//processing_queue->push(l);
		//processing_queue->finalise();
	} while (count > 0);
}

void process_requests(int client_socket, blocking_queue<work_item*>* processing_queue)
{
	work_item* l;
	do
	{
		l = processing_queue->pop();
		if (l != NULL)
		{
			int s = l->socket;
			printf("%s", l->buffer);
			//int count = send(client_socket, "200", 18, 0);
			delete l;
		}

	} while (l != NULL);

	closesocket(client_socket);
	printf("socket closed");
}

void accept_requests(int server_socket)
{
	struct sockaddr_in client_socket_address;
	int length = sizeof(client_socket_address);
	while (true)
	{
		int client_socket = accept(server_socket, (struct sockaddr *)&client_socket_address, &length);
		blocking_queue<work_item*>* processing_queue = new blocking_queue<work_item*>();
		std::thread handler_thread(handle_requests, client_socket, processing_queue);
		handler_thread.detach();
		std::thread process_thread(process_requests, client_socket, processing_queue);
		process_thread.detach();
	}
}


void start(int port)
{
	WSADATA wsaData;
	int listener;
	struct sockaddr_in serv_addr;
	port = 8221;

	WSAStartup(MAKEWORD(2, 2), &wsaData);
	listener = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
	memset((char *)&serv_addr, 0, sizeof(serv_addr));
	serv_addr.sin_family = AF_INET;
	serv_addr.sin_addr.s_addr = INADDR_ANY;
	serv_addr.sin_port = htons(port);

	bind(listener, (struct sockaddr *) &serv_addr, sizeof(serv_addr));
	listen(listener, 5);

	for (int i = 0; i < 1000; i++)
	{
		std::thread listener_thread(accept_requests, listener);
		listener_thread.detach();
	}

	Sleep(1000000 * 1000);
	WSACleanup();
}

int main()
{
	start(8221);
	return 0;
}



