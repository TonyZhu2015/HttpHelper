#include <iostream>
#include <X11/Xlib.h>

#include <cstring>
#include<stdio.h>

using namespace std;

void saveXImageToBitmap(XImage *pImage);

int main()
{
        Display *display;
        int screen;
        Window root;
        display = XOpenDisplay(0);
        screen = DefaultScreen(display);
        root = RootWindow(display, screen);
	XWindowAttributes gwa;

   XGetWindowAttributes(display, root, &gwa);
   int width = gwa.width;
   int height = gwa.height;
        XImage *img = XGetImage(display,root,0,0,width,height,XAllPlanes(),ZPixmap);

        if (img != NULL)
        {
saveXImageToBitmap(img);
           //save image here
        }
        return 0;
}

#pragma pack (1)
typedef struct BITMAPFILEHEADER 
{
short    bfType;
int    bfSize;
short    bfReserved1;
short    bfReserved2;
int   bfOffBits;
};

typedef struct BITMAPINFOHEADER
{
int  biSize;
int   biWidth;
int   biHeight;
short   biPlanes;
short   biBitCount;
int  biCompression;
int  biSizeImage;
int   biXPelsPerMeter;
int   biYPelsPerMeter;
int  biClrUsed;
int  biClrImportant;
};

void saveXImageToBitmap(XImage *pImage)
{
BITMAPFILEHEADER bmpFileHeader;
BITMAPINFOHEADER bmpInfoHeader;
FILE *fp;
static int cnt = 0;
int dummy;
char filePath[255];
memset(&bmpFileHeader, 0, sizeof(BITMAPFILEHEADER));
memset(&bmpInfoHeader, 0, sizeof(BITMAPINFOHEADER));
bmpFileHeader.bfType = 0x4D42;
bmpFileHeader.bfOffBits = sizeof(BITMAPFILEHEADER) + sizeof(BITMAPINFOHEADER);
bmpFileHeader.bfReserved1 = 0;
bmpFileHeader.bfReserved2 = 0;
int biBitCount =32;
int dwBmpSize = ((pImage->width * biBitCount + 31) / 32) * 4 * pImage->height;
printf("size of short:%d\r\n",(int)sizeof(short));
printf("size of int:%d\r\n",(int)sizeof(int));
printf("size of long:%d\r\n",(int)sizeof(long));
printf("dwBmpSize:%d\r\n",(int)dwBmpSize);
printf("BITMAPFILEHEADER:%d\r\n",(int)sizeof(BITMAPFILEHEADER));
printf("BITMAPINFOHEADER:%d\r\n",(int)sizeof(BITMAPINFOHEADER));
bmpFileHeader.bfSize = sizeof(BITMAPFILEHEADER) + sizeof(BITMAPINFOHEADER) +  dwBmpSize;

bmpInfoHeader.biSize = sizeof(BITMAPINFOHEADER);
bmpInfoHeader.biWidth = pImage->width;
bmpInfoHeader.biHeight = -pImage->height;
bmpInfoHeader.biPlanes = 1;
bmpInfoHeader.biBitCount = biBitCount;
bmpInfoHeader.biSizeImage = 0;
bmpInfoHeader.biCompression = 0;
bmpInfoHeader.biXPelsPerMeter = 0;
bmpInfoHeader.biYPelsPerMeter = 0;
bmpInfoHeader.biClrUsed = 0;
bmpInfoHeader.biClrImportant = 0;

sprintf(filePath, "bitmap%d.bmp", cnt++);
fp = fopen(filePath,"wb");

if(fp == NULL)
    return;

fwrite(&bmpFileHeader, sizeof(bmpFileHeader), 1, fp);
fwrite(&bmpInfoHeader, sizeof(bmpInfoHeader), 1, fp);
fwrite(pImage->data, dwBmpSize, 1, fp);
fclose(fp);
}
