﻿using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using UltimateFishBot.Classes.Helpers;

namespace UltimateFishBot.Classes.BodyParts
{
    class NoFishFoundException : Exception { }
    class Eyes
    {
        private Win32.CursorInfo m_noFishCursor;
        private IntPtr Wow;
        private Bitmap capturedCursorIcon;

        public Eyes(IntPtr wowWindow)
        {
            this.Wow = wowWindow;
        }

        public async Task<Win32.Point> LookForBobber(CancellationToken cancellationToken)
        {
            m_noFishCursor = Win32.GetNoFishCursor(this.Wow);
            Rectangle wowRectangle = Win32.GetWowRectangle(this.Wow);
            if (System.IO.File.Exists("capturedcursor.bmp")) {
                capturedCursorIcon = new Bitmap("capturedcursor.bmp", true);
            }

            Win32.Rect scanArea;
            if (!Properties.Settings.Default.customScanArea) {
                scanArea.Left = wowRectangle.X + wowRectangle.Width / 5;
                scanArea.Right = wowRectangle.X + wowRectangle.Width / 5 * 4;
                scanArea.Top = wowRectangle.Y + wowRectangle.Height / 4;
                scanArea.Bottom = wowRectangle.Y + wowRectangle.Height / 4 * 3;
                Log.Information("Using default area");
            } else {
                scanArea.Left = Properties.Settings.Default.minScanXY.X;
                scanArea.Top = Properties.Settings.Default.minScanXY.Y;
                scanArea.Right = Properties.Settings.Default.maxScanXY.X;
                scanArea.Bottom = Properties.Settings.Default.maxScanXY.Y;
                Log.Information("Using custom area");
            }
            Log.Information("Scanning area: " + scanArea.Left.ToString() + " , " + scanArea.Top.ToString() + " , " + scanArea.Right.ToString() + " , " + scanArea.Bottom.ToString());
            Win32.Point bobberPos;
            bobberPos.x = 0;
            bobberPos.y = 0;
            if (Properties.Settings.Default.AlternativeRoute)
                bobberPos = await LookForBobberSpiralImpl(scanArea, bobberPos, Properties.Settings.Default.ScanningSteps, Properties.Settings.Default.ScanningRetries, cancellationToken);
            else
                bobberPos = await LookForBobberImpl(scanArea, bobberPos, Properties.Settings.Default.ScanningSteps, Properties.Settings.Default.ScanningRetries, cancellationToken);

            Log.Information("Bobber scan end. ({bx},{by})", bobberPos.x, bobberPos.y);
            return bobberPos;

        }

        public async Task<bool> SetMouseToBobber(Win32.Point bobberPos, CancellationToken cancellationToken)  {// move mouse to previous recorded position and check shape
            if (!await MoveMouseAndCheckCursor(bobberPos.x, bobberPos.y, cancellationToken)) {
                Log.Information("Bobber lost. ({bx},{by})", bobberPos.x, bobberPos.y);
                int fixr = 24;
                Win32.Rect scanArea;
                scanArea.Left = bobberPos.x - fixr;
                scanArea.Right = bobberPos.x + fixr;
                scanArea.Top = bobberPos.y - fixr;
                scanArea.Bottom = bobberPos.y + fixr;
                // initiate a small-area search for bobber
                Win32.Point npos;
                npos.x = 0;
                npos.y = 0;
                npos = await LookForBobberSpiralImpl(scanArea, npos,4,1,cancellationToken);
                if (npos.x != 0 && npos.y != 0) {
                    // search was successful
                    Log.Information("Bobber found. ({bx},{by})", npos.x, npos.y);
                    return true;
                } else {
                    Log.Information("Bobber flost. ({bx},{by})", npos.x, npos.y);
                    return false;
                }
            }
            return true;
        }


        private async Task<Win32.Point> LookForBobberImpl(Win32.Rect scanArea, Win32.Point bobberPos, int steps, int retries, CancellationToken cancellationToken) {

            int XPOSSTEP = (int)((scanArea.Right - scanArea.Left) / steps);
            int YPOSSTEP = (int)((scanArea.Bottom - scanArea.Top) / steps);
            int XOFFSET = (int)(XPOSSTEP / retries);

            for (int tryCount = 0; tryCount < retries; ++tryCount) {
                for (int x = (int)(scanArea.Left + (XOFFSET * tryCount)); x < scanArea.Right; x += XPOSSTEP) {
                    for (int y = scanArea.Top; y < scanArea.Bottom; y += YPOSSTEP) {
                        if (await MoveMouseAndCheckCursor(x, y, cancellationToken)) {
                            bobberPos.x = x;
                            bobberPos.y = y;
                            return bobberPos;
                        }
                    }
                }
            }
            return bobberPos;
        }

        private async Task<Win32.Point> LookForBobberSpiralImpl(Win32.Rect scanArea, Win32.Point bobberPos, int steps, int retries, CancellationToken cancellationToken) {

            int XPOSSTEP = (int)((scanArea.Right - scanArea.Left) / steps);
            int YPOSSTEP = (int)((scanArea.Bottom - scanArea.Top) / steps);
            int XOFFSET = (int)(XPOSSTEP / retries);
            int YOFFSET = (int)(YPOSSTEP / retries);

            for (int tryCount = 0; tryCount < retries; ++tryCount) {
                int x = (int)((scanArea.Left + scanArea.Right) / 2) + XOFFSET * tryCount;
                int y = (int)((scanArea.Top + scanArea.Bottom) / 2) + YOFFSET * tryCount;

                for (int i = 0; i <= 2 * steps; i++) {
                    for (int j = 0; j <= (i / 2); j++) {
                        int dx = 0, dy = 0;
                        if (i % 2 == 0) {
                            if ((i / 2) % 2 == 0) {
                                dx = XPOSSTEP;
                                dy = 0;
                            } else {
                                dx = -XPOSSTEP;
                                dy = 0;
                            }
                        } else {
                            if ((i / 2) % 2 == 0) {
                                dx = 0;
                                dy = YPOSSTEP;
                            } else {
                                dx = 0;
                                dy = -YPOSSTEP;
                            }
                        }
                        x += dx;
                        y += dy;
                        if (await MoveMouseAndCheckCursor(x, y, cancellationToken)) {
                            bobberPos.x = x;
                            bobberPos.y = y;
                            return bobberPos;
                        }
                    }
                }
            }
            return bobberPos;
        }

        private async Task<bool> MoveMouseAndCheckCursor(int x, int y, CancellationToken cancellationToken)   {
            if (cancellationToken.IsCancellationRequested)
                throw new TaskCanceledException();

            Win32.MoveMouse(x, y);

            // Pause (give the OS a chance to change the cursor)
            await Task.Delay(Properties.Settings.Default.ScanningDelay, cancellationToken);

            Win32.CursorInfo actualCursor = Win32.GetCurrentCursor();

            if (actualCursor.flags == m_noFishCursor.flags &&
                actualCursor.hCursor == m_noFishCursor.hCursor)
                return false;

            // Compare the actual icon with our fishIcon if user want it
            if (Properties.Settings.Default.CheckCursor) { 
                if (ImageCompare(Win32.GetCursorIcon(actualCursor), Properties.Resources.fishIcon35x35)) { 
                    // We found a fish!
                    return true;
                }
                if (capturedCursorIcon != null && ImageCompare(Win32.GetCursorIcon(actualCursor), capturedCursorIcon)) {
                    // We found a fish!
                    return true;
                }
                return false;
            }

            return true;
        }


        private static bool ImageCompare(Bitmap bmp1, Bitmap bmp2)  {

            if (bmp1 == null || bmp2 == null) { 
                return false;
            }
            if (object.Equals(bmp1, bmp2)) { 
                return true;
            }
            if (!bmp1.Size.Equals(bmp2.Size) || !bmp1.PixelFormat.Equals(bmp2.PixelFormat)) { 
                return false;
            }

            int bytes = bmp1.Width * bmp1.Height * (Image.GetPixelFormatSize(bmp1.PixelFormat) / 8);

            bool result = true;
            byte[] b1bytes = new byte[bytes];
            byte[] b2bytes = new byte[bytes];

            BitmapData bitmapData1 = bmp1.LockBits(new Rectangle(0, 0, bmp1.Width - 1, bmp1.Height - 1), ImageLockMode.ReadOnly, bmp1.PixelFormat);
            BitmapData bitmapData2 = bmp2.LockBits(new Rectangle(0, 0, bmp2.Width - 1, bmp2.Height - 1), ImageLockMode.ReadOnly, bmp2.PixelFormat);

            Marshal.Copy(bitmapData1.Scan0, b1bytes, 0, bytes);
            Marshal.Copy(bitmapData2.Scan0, b2bytes, 0, bytes);

            for (int n = 0; n <= bytes - 1; n++) {
                if (b1bytes[n] != b2bytes[n]) {
                    result = false;
                    break;
                }
            }

            bmp1.UnlockBits(bitmapData1);
            bmp2.UnlockBits(bitmapData2);

            return result;
        }

        public void CaptureCursor() {
            Win32.CursorInfo actualCursor = Win32.GetCurrentCursor();
            Bitmap cursorIcon = Win32.GetCursorIcon(actualCursor);
            cursorIcon.Save("capturedcursor.bmp");
        }

    }
}