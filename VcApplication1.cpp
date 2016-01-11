#include <iostream>
#include <fstream>
#include <string>
#include <thread>

#include <queue>
#include <mutex>
#include <condition_variable>

#include <winsock2.h>
#pragma comment(lib, "Ws2_32.lib")

class WorkItem
{
public:
	int socket;
	char buffer[256];

	WorkItem();
	~WorkItem();

private:
};

WorkItem::WorkItem()
{
}

WorkItem::~WorkItem()
{
}

template <typename T> class BlockingQueue
{
public:

	T pop()
	{
		T item;
		if (!completed)
		{
			std::unique_lock<std::mutex> mlock(mutex_);
			while (queue_.empty() && !completed)
			{
				cond_.wait(mlock);
			}

			if (!completed)
			{
				item = queue_.front();
				queue_.pop();
			}
		}

		return item;
	}

	void pop(T& item)
	{
		std::unique_lock<std::mutex> mlock(mutex_);
		while (queue_.empty())
		{
			cond_.wait(mlock);
		}

		item = queue_.front();
		queue_.pop();
	}

	void push(T& item)
	{
		std::unique_lock<std::mutex> mlock(mutex_);
		queue_.push(item);
		mlock.unlock();
		cond_.notify_one();
	}

	void push(T&& item)
	{
		std::unique_lock<std::mutex> mlock(mutex_);
		queue_.push(std::move(item));
		mlock.unlock();
		cond_.notify_one();
	}

	void finalise()
	{
		completed = true;
		cond_.notify_all();
	}

private:
	std::queue<T> queue_;
	std::mutex mutex_;
	std::condition_variable cond_;
	bool completed;
};

void handle_requests(int client_socket, BlockingQueue<WorkItem>* processing_queue)
{
	char buffer[256];
	int  count;
	do
	{
		memset(buffer, 0, 256);
		count = recv(client_socket, buffer, 255, 0);
		WorkItem* l = new WorkItem();
		l->socket = client_socket;
		strcpy(l->buffer, buffer);
		processing_queue->push(*l);
		processing_queue->finalise();
	} while (count > 0);
}

void process_requests(int client_socket, BlockingQueue<WorkItem>* processing_queue)
{
	WorkItem l;
	bool process = true;
	do
	{
		l = processing_queue->pop();
		process = &l != nullptr;
		if (process)
		{
			int s = l.socket;
			printf("%s", l.buffer);
			int count = send(client_socket, "200", 18, 0);
			//delete &l;
		}

	} while (process);

	closesocket(client_socket);
}

void accept_requests(int server_socket)
{
	struct sockaddr_in client_socket_address;
	int length = sizeof(client_socket_address);
	while (true)
	{
		int client_socket = accept(server_socket, (struct sockaddr *)&client_socket_address, &length);	
		BlockingQueue<WorkItem>* processing_queue = new BlockingQueue<WorkItem>();	
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



