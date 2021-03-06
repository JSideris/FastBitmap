/*
    The MIT License (MIT)
    
    Copyright (c) 2014 Luiz Fernando Silva
    
    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:
    
    The above copyright notice and this permission notice shall be included in
    all copies or substantial portions of the Software.
    
    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
    THE SOFTWARE.
*/

using System;
using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FastBitmap.Utils
{
    /// <summary>
    /// Contains tests for the FastBitmap class and related components
    /// </summary>
    [TestClass]
    public class FastBitmapTests
    {
        [TestMethod]
        [ExpectedException(typeof (ArgumentException),
            "Providing a bitmap with a bitdepth different than 32bpp to a FastBitmap must return an ArgumentException")]
        public void TestFastBitmapCreation()
        {
            Bitmap bitmap = new Bitmap(64, 64);
            FastBitmap fastBitmap = new FastBitmap(bitmap);
            fastBitmap.Lock();
            fastBitmap.Unlock();

            // Try creating a FastBitmap with different 32bpp depths
            try
            {
                // ReSharper disable once ObjectCreationAsStatement
                new FastBitmap(new Bitmap(1, 1, PixelFormat.Format32bppArgb));
                // ReSharper disable once ObjectCreationAsStatement
                new FastBitmap(new Bitmap(1, 1, PixelFormat.Format32bppPArgb));
                // ReSharper disable once ObjectCreationAsStatement
                new FastBitmap(new Bitmap(1, 1, PixelFormat.Format32bppRgb));
            }
            catch (ArgumentException)
            {
                Assert.Fail("The FastBitmap should accept any type of 32bpp pixel format bitmap");
            }

            // Try creating a FastBitmap with a bitmap of a bit depth different from 32bpp
            Bitmap invalidBitmap = new Bitmap(64, 64, PixelFormat.Format4bppIndexed);

            // ReSharper disable once ObjectCreationAsStatement
            new FastBitmap(invalidBitmap);
        }

        /// <summary>
        /// Tests sequential instances of FastBitmaps on the same Bitmap.
        /// As long as all the operations pending on a fast bitmap are finished, the original bitmap can be used in as many future fast bitmaps as needed.
        /// </summary>
        [TestMethod]
        public void TestSequentialFastBitmapLocking()
        {
            Bitmap bitmap = new Bitmap(64, 64);
            FastBitmap fastBitmap = new FastBitmap(bitmap);

            Assert.IsFalse(fastBitmap.Locked, "Immediately after creation, the FastBitmap.Locked property must be false");

            fastBitmap.Lock();

            Assert.IsTrue(fastBitmap.Locked, "After a successful call to .Lock(), the .Locked property must be true");

            fastBitmap.Unlock();

            Assert.IsFalse(fastBitmap.Locked, "After a successful call to .Lock(), the .Locked property must be false");

            fastBitmap = new FastBitmap(bitmap);
            fastBitmap.Lock();
            fastBitmap.Unlock();
        }

        /// <summary>
        /// Tests a failing scenario for fast bitmap creations where a sequential fast bitmap is created and locked while another fast bitmap is operating on the same bitmap
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException), "Trying to Lock() a bitmap while it is Locked() in another FastBitmap must raise an exception")]
        public void TestFailedSequentialFastBitmapLocking()
        {
            Bitmap bitmap = new Bitmap(64, 64);
            FastBitmap fastBitmap = new FastBitmap(bitmap);
            fastBitmap.Lock();

            fastBitmap = new FastBitmap(bitmap);
            fastBitmap.Lock();
        }

        /// <summary>
        /// Tests the behavior of the .Clear() instance and class methods by clearing a bitmap and checking the result pixel-by-pixel
        /// </summary>
        [TestMethod]
        public void TestClearBitmap()
        {
            Bitmap bitmap = GenerateRandomBitmap(63, 63); // Non-dibisible by 8 bitmap, used to test loop unrolling
            FastBitmap.ClearBitmap(bitmap, Color.Red);

            // Loop through the image checking the pixels now
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    if (bitmap.GetPixel(x, y).ToArgb() != Color.Red.ToArgb())
                    {
                        Assert.Fail(
                            "Immediately after a call to FastBitmap.Clear(), all of the bitmap's pixels must be of the provided color");
                    }
                }
            }

            // Test an arbitratry color now
            FastBitmap.ClearBitmap(bitmap, Color.FromArgb(25, 12, 0, 42));

            // Loop through the image checking the pixels now
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    if (bitmap.GetPixel(x, y).ToArgb() != Color.FromArgb(25, 12, 0, 42).ToArgb())
                    {
                        Assert.Fail(
                            "Immediately after a call to FastBitmap.Clear(), all of the bitmap's pixels must be of the provided color");
                    }
                }
            }

            // Test instance call
            FastBitmap fastBitmap = new FastBitmap(bitmap);
            fastBitmap.Clear(Color.FromArgb(25, 12, 0, 42));

            Assert.IsFalse(fastBitmap.Locked, "After a successfull call to .Clear() on a fast bitmap previously unlocked, the .Locked property must be false");

            // Loop through the image checking the pixels now
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    if (bitmap.GetPixel(x, y).ToArgb() != Color.FromArgb(25, 12, 0, 42).ToArgb())
                    {
                        Assert.Fail(
                            "Immediately after a call to FastBitmap.Clear(), all of the bitmap's pixels must be of the provided color");
                    }
                }
            }
        }

        /// <summary>
        /// Tests the behavior of the GetPixel() method by comparing the results from it to the results of the native Bitmap.GetPixel()
        /// </summary>
        [TestMethod]
        public void TestGetPixel()
        {
            Bitmap original = GenerateRandomBitmap(64, 64);
            Bitmap copy = original.Clone(new Rectangle(0, 0, 64, 64), original.PixelFormat);

            FastBitmap fastOriginal = new FastBitmap(original);
            fastOriginal.Lock();

            for (int y = 0; y < original.Height; y++)
            {
                for (int x = 0; x < original.Width; x++)
                {
                    Assert.AreEqual(fastOriginal.GetPixel(x, y).ToArgb(), copy.GetPixel(x, y).ToArgb(),
                        "Calls to FastBitmap.GetPixel() must return the same value as returned by Bitmap.GetPixel()");
                }
            }

            fastOriginal.Unlock();
        }

        /// <summary>
        /// Tests the behavior of the SetPixel() method by randomly filling two bitmaps via native SetPixel and the implemented SetPixel, then comparing the output similarity
        /// </summary>
        [TestMethod]
        public void TestSetPixel()
        {
            Bitmap bitmap1 = new Bitmap(64, 64);
            Bitmap bitmap2 = new Bitmap(64, 64);

            FastBitmap fastBitmap1 = new FastBitmap(bitmap1);
            fastBitmap1.Lock();

            Random r = new Random();

            for (int y = 0; y < bitmap1.Height; y++)
            {
                for (int x = 0; x < bitmap1.Width; x++)
                {
                    int intColor = r.Next(0xFFFFFF);
                    Color color = Color.FromArgb(intColor);

                    fastBitmap1.SetPixel(x, y, color);
                    bitmap2.SetPixel(x, y, color);
                }
            }

            fastBitmap1.Unlock();

            AssertBitmapEquals(bitmap1, bitmap2,
                "Calls to FastBitmap.SetPixel() must be equivalent to calls to Bitmap.SetPixel()");
        }

        /// <summary>
        /// Tests the behavior of the SetPixel() integer overload method by randomly filling two bitmaps via native SetPixel and the implemented SetPixel, then comparing the output similarity
        /// </summary>
        [TestMethod]
        public void TestSetPixelInt()
        {
            Bitmap bitmap1 = new Bitmap(64, 64);
            Bitmap bitmap2 = new Bitmap(64, 64);

            FastBitmap fastBitmap1 = new FastBitmap(bitmap1);
            fastBitmap1.Lock();

            Random r = new Random();

            for (int y = 0; y < bitmap1.Height; y++)
            {
                for (int x = 0; x < bitmap1.Width; x++)
                {
                    int intColor = r.Next(0xFFFFFF);
                    Color color = Color.FromArgb(intColor);

                    fastBitmap1.SetPixel(x, y, intColor);
                    bitmap2.SetPixel(x, y, color);
                }
            }

            fastBitmap1.Unlock();

            AssertBitmapEquals(bitmap1, bitmap2,
                "Calls to FastBitmap.SetPixel() with an integer overload must be equivalent to calls to Bitmap.SetPixel() with a Color with the same ARGB value as the interger");
        }

        /// <summary>
        /// Tests a call to FastBitmap.CopyPixels() with valid provided bitmaps
        /// </summary>
        [TestMethod]
        public void TestValidCopyPixels()
        {
            Bitmap bitmap1 = GenerateRandomBitmap(64, 64);
            Bitmap bitmap2 = new Bitmap(64, 64);

            FastBitmap.CopyPixels(bitmap1, bitmap2);

            AssertBitmapEquals(bitmap1, bitmap2,
                "After a successful call to CopyPixels(), both bitmaps must be equal down to the pixel level");
        }

        /// <summary>
        /// Tests a call to FastBitmap.CopyPixels() with bitmaps of different sizes and different bitdepths
        /// </summary>
        [TestMethod]
        public void TestInvalidCopyPixels()
        {
            Bitmap bitmap1 = new Bitmap(64, 64, PixelFormat.Format24bppRgb);
            Bitmap bitmap2 = new Bitmap(64, 64, PixelFormat.Format1bppIndexed);

            if (FastBitmap.CopyPixels(bitmap1, bitmap2))
            {
                Assert.Fail("Trying to copy two bitmaps of different bitdepths should not be allowed");
            }

            bitmap1 = new Bitmap(64, 64, PixelFormat.Format32bppArgb);
            bitmap2 = new Bitmap(66, 64, PixelFormat.Format32bppArgb);

            if (FastBitmap.CopyPixels(bitmap1, bitmap2))
            {
                Assert.Fail("Trying to copy two bitmaps of different sizes should not be allowed");
            }
        }

        #region CopyRegion Tests

        /// <summary>
        /// Tests the CopyRegion() static and instance methods by creating two bitmaps, copying regions over from one to another, and comparing the expected pixel equalities
        /// </summary>
        [TestMethod]
        public void TestSimpleCopyRegion()
        {
            Bitmap canvasBitmap = new Bitmap(64, 64);
            Bitmap copyBitmap = GenerateRandomBitmap(32, 32);

            Rectangle sourceRectangle = new Rectangle(0, 0, 32, 32);
            Rectangle targetRectangle = new Rectangle(0, 0, 64, 64);

            FastBitmap.CopyRegion(copyBitmap, canvasBitmap, sourceRectangle, targetRectangle);

            AssertCopyRegionEquals(canvasBitmap, copyBitmap, targetRectangle, sourceRectangle);
        }

        /// <summary>
        /// Tests the CopyRegion() static and instance methods by creating two bitmaps, copying regions over from one to another, and comparing the expected pixel equalities.
        /// The source and target rectangles are moved around, and the source rectangle clips outside the bounds of the copy bitmap
        /// </summary>
        [TestMethod]
        public void TestComplexCopyRegion()
        {
            Bitmap canvasBitmap = new Bitmap(64, 64);
            Bitmap copyBitmap = GenerateRandomBitmap(32, 32);

            Rectangle sourceRectangle = new Rectangle(5, 5, 32, 32);
            Rectangle targetRectangle = new Rectangle(9, 9, 23, 48);

            FastBitmap.CopyRegion(copyBitmap, canvasBitmap, sourceRectangle, targetRectangle);

            AssertCopyRegionEquals(canvasBitmap, copyBitmap, targetRectangle, sourceRectangle);
        }

        /// <summary>
        /// Tests the CopyRegion() static and instance methods by creating two bitmaps, copying regions over from one to another, and comparing the expected pixel equalities.
        /// The copy region clips outside the target and source bitmap areas
        /// </summary>
        [TestMethod]
        public void TestClippingCopyRegion()
        {
            Bitmap canvasBitmap = new Bitmap(64, 64);
            Bitmap copyBitmap = GenerateRandomBitmap(32, 32);

            Rectangle sourceRectangle = new Rectangle(-5, 5, 32, 32);
            Rectangle targetRectangle = new Rectangle(40, 9, 23, 48);

            FastBitmap.CopyRegion(copyBitmap, canvasBitmap, sourceRectangle, targetRectangle);

            AssertCopyRegionEquals(canvasBitmap, copyBitmap, targetRectangle, sourceRectangle);
        }

        /// <summary>
        /// Tests the CopyRegion() static and instance methods by creating two bitmaps, copying regions over from one to another, and comparing the expected pixel equalities.
        /// The source region provided is out of the bounds of the copy image
        /// </summary>
        [TestMethod]
        public void TestOutOfBoundsCopyRegion()
        {
            Bitmap canvasBitmap = new Bitmap(64, 64);
            Bitmap copyBitmap = GenerateRandomBitmap(32, 32);

            Rectangle sourceRectangle = new Rectangle(32, 0, 32, 32);
            Rectangle targetRectangle = new Rectangle(0, 0, 23, 48);

            FastBitmap.CopyRegion(copyBitmap, canvasBitmap, sourceRectangle, targetRectangle);

            AssertCopyRegionEquals(canvasBitmap, copyBitmap, targetRectangle, sourceRectangle);
        }

        /// <summary>
        /// Tests the CopyRegion() static and instance methods by creating two bitmaps, copying regions over from one to another, and comparing the expected pixel equalities.
        /// The source region provided is invalid, and no modifications are to be made
        /// </summary>
        [TestMethod]
        public void TestInvalidCopyRegion()
        {
            Bitmap canvasBitmap = new Bitmap(64, 64);
            Bitmap copyBitmap = GenerateRandomBitmap(32, 32);

            Rectangle sourceRectangle = new Rectangle(0, 0, -1, 32);
            Rectangle targetRectangle = new Rectangle(0, 0, 23, 48);

            FastBitmap.CopyRegion(copyBitmap, canvasBitmap, sourceRectangle, targetRectangle);

            AssertCopyRegionEquals(canvasBitmap, copyBitmap, targetRectangle, sourceRectangle);
        }

        /// <summary>
        /// Tests sequential region copying across multiple bitmaps by copying regions between 4 bitmaps
        /// </summary>
        [TestMethod]
        public void TestSequentialCopyRegion()
        {
            Bitmap bitmap1 = new Bitmap(64, 64);
            Bitmap bitmap2 = new Bitmap(64, 64);
            Bitmap bitmap3 = new Bitmap(64, 64);
            Bitmap bitmap4 = new Bitmap(64, 64);

            Rectangle region = new Rectangle(0, 0, 64, 64);

            FastBitmap.CopyRegion(bitmap1, bitmap2, region, region);
            FastBitmap.CopyRegion(bitmap3, bitmap4, region, region);
            FastBitmap.CopyRegion(bitmap1, bitmap3, region, region);
            FastBitmap.CopyRegion(bitmap4, bitmap2, region, region);
        }

        /// <summary>
        /// Tests a copy region operation that is slices through the destination
        /// </summary>
        [TestMethod]
        public void TestSlicedDestinationCopyRegion()
        {
            // Have a copy operation that goes:
            //
            //       -src---
            // -dest-|-----|------
            // |     |xxxxx|     |
            // |     |xxxxx|     |
            // ------|-----|------
            //       -------
            // 

            Bitmap canvasBitmap = new Bitmap(128, 32);
            Bitmap copyBitmap = GenerateRandomBitmap(32, 64);

            Rectangle sourceRectangle = new Rectangle(0, 0, 32, 64);
            Rectangle targetRectangle = new Rectangle(32, -16, 32, 64);

            FastBitmap.CopyRegion(copyBitmap, canvasBitmap, sourceRectangle, targetRectangle);

            AssertCopyRegionEquals(canvasBitmap, copyBitmap, targetRectangle, sourceRectangle);
        }

        #endregion

        /// <summary>
        /// Tests the FastBitmapLocker struct returned by lock calls
        /// </summary>
        [TestMethod]
        public void TestFastBitmapLocker()
        {
            Bitmap bitmap = new Bitmap(64, 64);
            FastBitmap fastBitmap = new FastBitmap(bitmap);

            // Immediate lock and dispose
            fastBitmap.Lock().Dispose();
            Assert.IsFalse(fastBitmap.Locked, "After disposing of the FastBitmapLocker object, the underlying fast bitmap must be unlocked");

            using (var locker = fastBitmap.Lock())
            {
                fastBitmap.SetPixel(0, 0, 0);

                Assert.AreEqual(fastBitmap, locker.FastBitmap, "The fast bitmap referenced in the fast bitmap locker must be the one that had the original Lock() call");
            }

            Assert.IsFalse(fastBitmap.Locked, "After disposing of the FastBitmapLocker object, the underlying fast bitmap must be unlocked");

            // Test the conditional unlocking of the fast bitmap locker by unlocking the fast bitmap before exiting the 'using' block
            using (fastBitmap.Lock())
            {
                fastBitmap.SetPixel(0, 0, 0);
                fastBitmap.Unlock();
            }
        }

        [TestMethod]
        public void TestLockExtensionMethod()
        {
            Bitmap bitmap = new Bitmap(64, 64);

            using (FastBitmap fast = bitmap.FastLock())
            {
                fast.SetPixel(0, 0, Color.Red);
            }

            // Test unlocking by trying to modify the bitmap
            bitmap.SetPixel(0, 0, Color.Blue);
        }

        [TestMethod]
        public void TestDataArray()
        {
            // TODO: Devise a way to test the returned array in a more consistent way, because currently this test only deals with ARGB pixel values because Bitmap.GetPixel().ToArgb() only returns 0xAARRGGBB format values
            Bitmap bitmap = GenerateRandomBitmap(64, 64);
            FastBitmap fastBitmap = new FastBitmap(bitmap);

            Assert.IsFalse(fastBitmap.Locked, "After accessing the .Data property on a fast bitmap previously unlocked, the .Locked property must be false");

            int[] pixels = fastBitmap.DataArray;

            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    Assert.AreEqual(bitmap.GetPixel(x, y).ToArgb(), pixels[y * bitmap.Width + x], "");
                }
            }
        }

        [TestMethod]
        public void TestCopyFromArray()
        {
            Bitmap bitmap = new Bitmap(4, 4);
            int[] colors =
            {
                0xFFFFFF, 0xFFFFEF, 0xABABAB, 0xABCDEF,
                0x111111, 0x123456, 0x654321, 0x000000,
                0xFFFFFF, 0xFFFFEF, 0xABABAB, 0xABCDEF,
                0x111111, 0x123456, 0x654321, 0x000000
            };

            using (var fastBitmap = bitmap.FastLock())
            {
                fastBitmap.CopyFromArray(colors);
            }

            // Test now the resulting bitmap
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    int index = y * bitmap.Width + x;

                    Assert.AreEqual(colors[index], bitmap.GetPixel(x, y).ToArgb(),
                        "After a call to CopyFromArray, the values provided on the on the array must match the values in the bitmap pixels");
                }
            }
        }

        [TestMethod]
        public void TestCopyFromArrayIgnoreZeroes()
        {
            Bitmap bitmap = new Bitmap(4, 4);

            FillBitmapRegion(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height), Color.Red);

            int[] colors =
            {
                0xFFFFFF, 0xFFFFEF, 0xABABAB, 0xABCDEF,
                0x111111, 0x123456, 0x654321, 0x000000,
                0x000000, 0xFFFFEF, 0x000000, 0xABCDEF,
                0x000000, 0x000000, 0x654321, 0x000000
            };

            using (var fastBitmap = bitmap.FastLock())
            {
                fastBitmap.CopyFromArray(colors, true);
            }

            // Test now the resulting bitmap
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    int index = y * bitmap.Width + x;
                    int arrayColor = colors[index];
                    int bitmapColor = bitmap.GetPixel(x, y).ToArgb();

                    if(arrayColor != 0)
                    {
                        Assert.AreEqual(arrayColor, bitmapColor,
                            "After a call to CopyFromArray(_, true), the non-zeroes values provided on the on the array must match the values in the bitmap pixels");
                    }
                    else
                    {
                        Assert.AreEqual(Color.Red.ToArgb(), bitmapColor,
                            "After a call to CopyFromArray(_, true), the 0 values on the original array must not be copied over");
                    }
                }
            }
        }

        #region Exception Tests

        [TestMethod]
        [ExpectedException(typeof (InvalidOperationException),
            "When trying to unlock a FastBitmap that is not locked, an exception must be thrown")]
        public void TestFastBitmapUnlockingException()
        {
            Bitmap bitmap = new Bitmap(64, 64);
            FastBitmap fastBitmap = new FastBitmap(bitmap);

            fastBitmap.Unlock();
        }

        [TestMethod]
        [ExpectedException(typeof (InvalidOperationException),
            "When trying to lock a FastBitmap that is already locked, an exception must be thrown")]
        public void TestFastBitmapLockingException()
        {
            Bitmap bitmap = new Bitmap(64, 64);
            FastBitmap fastBitmap = new FastBitmap(bitmap);

            fastBitmap.Lock();
            fastBitmap.Lock();
        }

        [TestMethod]
        [ExpectedException(typeof (InvalidOperationException),
            "When trying to read or write to the FastBitmap via GetPixel while it is unlocked, an exception must be thrown"
            )]
        public void TestFastBitmapUnlockedGetAccessException()
        {
            Bitmap bitmap = new Bitmap(64, 64);
            FastBitmap fastBitmap = new FastBitmap(bitmap);

            fastBitmap.GetPixel(0, 0);
        }

        [TestMethod]
        [ExpectedException(typeof (InvalidOperationException),
            "When trying to read or write to the FastBitmap via SetPixel while it is unlocked, an exception must be thrown"
            )]
        public void TestFastBitmapUnlockedSetAccessException()
        {
            Bitmap bitmap = new Bitmap(64, 64);
            FastBitmap fastBitmap = new FastBitmap(bitmap);

            fastBitmap.SetPixel(0, 0, 0);
        }

        [TestMethod]
        public void TestFastBitmapGetPixelBoundsException()
        {
            Bitmap bitmap = new Bitmap(64, 64);
            FastBitmap fastBitmap = new FastBitmap(bitmap);

            fastBitmap.Lock();

            try
            {
                fastBitmap.GetPixel(-1, -1);
                Assert.Fail("When trying to access a coordinate that is out of bounds via GetPixel, an exception must be thrown");
            }
            catch (ArgumentOutOfRangeException) { }

            try
            {
                fastBitmap.GetPixel(fastBitmap.Width, 0);
                Assert.Fail("When trying to access a coordinate that is out of bounds via GetPixel, an exception must be thrown");
            }
            catch (ArgumentOutOfRangeException) { }

            try
            {
                fastBitmap.GetPixel(0, fastBitmap.Height);
                Assert.Fail("When trying to access a coordinate that is out of bounds via GetPixel, an exception must be thrown");
            }
            catch (ArgumentOutOfRangeException) { }

            fastBitmap.GetPixel(fastBitmap.Width - 1, fastBitmap.Height - 1);
        }

        [TestMethod]
        public void TestFastBitmapSetPixelBoundsException()
        {
            Bitmap bitmap = new Bitmap(64, 64);
            FastBitmap fastBitmap = new FastBitmap(bitmap);

            fastBitmap.Lock();

            try
            {
                fastBitmap.SetPixel(-1, -1, 0);
                Assert.Fail("When trying to access a coordinate that is out of bounds via GetPixel, an exception must be thrown");
            }
            catch (ArgumentOutOfRangeException) { }

            try
            {
                fastBitmap.SetPixel(fastBitmap.Width, 0, 0);
                Assert.Fail("When trying to access a coordinate that is out of bounds via GetPixel, an exception must be thrown");
            }
            catch (ArgumentOutOfRangeException) { }

            try
            {
                fastBitmap.SetPixel(0, fastBitmap.Height, 0);
                Assert.Fail("When trying to access a coordinate that is out of bounds via GetPixel, an exception must be thrown");
            }
            catch (ArgumentOutOfRangeException) { }

            fastBitmap.SetPixel(fastBitmap.Width - 1, fastBitmap.Height - 1, 0);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException), "An ArgumentException exception must be thrown when trying to copy regions across the same bitmap")]
        public void TestSameBitmapCopyRegionException()
        {
            Bitmap bitmap = new Bitmap(64, 64);

            Rectangle sourceRectangle = new Rectangle(0, 0, 64, 64);
            Rectangle targetRectangle = new Rectangle(0, 0, 64, 64);

            FastBitmap.CopyRegion(bitmap, bitmap, sourceRectangle, targetRectangle);
        }

        [TestMethod]
        [ExpectedException(typeof (ArgumentException),
            "an ArgumentException exception must be raised when calling CopyFromArray() with an array of colors that does not match the pixel count of the bitmap")]
        public void TestCopyFromArrayMismatchedLengthException()
        {
            Bitmap bitmap = new Bitmap(4, 4);

            FillBitmapRegion(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height), Color.Red);

            int[] colors =
            {
                0xFFFFFF, 0xFFFFEF, 0xABABAB, 0xABCDEF,
                0x111111, 0x123456, 0x654321, 0x000000,
                0x000000, 0xFFFFEF, 0x000000, 0xABCDEF,
                0x000000, 0x000000, 0x654321, 0x000000,
                0x000000, 0x000000, 0x654321, 0x000000
            };

            using (var fastBitmap = bitmap.FastLock())
            {
                fastBitmap.CopyFromArray(colors, true);
            }
        }

        #endregion

        /// <summary>
        /// Generates a frame image with a given set of parameters.
        /// The seed is used to randomize the frame, and any call with the same width, height and seed will generate the same image
        /// </summary>
        /// <param name="width">The width of the image to generate</param>
        /// <param name="height">The height of the image to generate</param>
        /// <param name="seed">The seed for the image, used to seed the random number generator that will generate the image contents</param>
        /// <returns>An image with the passed parameters</returns>
        public static Bitmap GenerateRandomBitmap(int width, int height, int seed = -1)
        {
            if (seed == -1)
            {
                seed = _seedRandom.Next();
            }
            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            FastBitmap fastBitmap = new FastBitmap(bitmap);
            fastBitmap.Lock();
            // Plot the image with random pixels now
            Random r = new Random(seed);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    uint pixelColor = (uint)(r.NextDouble() * 0xFFFFFFFF);
                    fastBitmap.SetPixel(x, y, pixelColor);
                }
            }
            fastBitmap.Unlock();
            return bitmap;
        }

        /// <summary>
        /// Fills a rectangle region of bitmap with a specified color
        /// </summary>
        /// <param name="bitmap">The bitmap to operate on</param>
        /// <param name="region">The region to fill on the bitmap</param>
        /// <param name="color">The color to fill the bitmap with</param>
        public static void FillBitmapRegion(Bitmap bitmap, Rectangle region, Color color)
        {
            for (int y = Math.Max(0, region.Top); y < Math.Min(bitmap.Height, region.Bottom); y++)
            {
                for (int x = Math.Max(0, region.Left); x < Math.Min(bitmap.Width, region.Right); x++)
                {
                    bitmap.SetPixel(x, y, color);
                }
            }
        }

        /// <summary>
        /// Helper method that tests the equality of two bitmaps and fails with a provided assert message when they are not pixel-by-pixel equal
        /// </summary>
        /// <param name="bitmap1">The first bitmap object to compare</param>
        /// <param name="bitmap2">The second bitmap object to compare</param>
        /// <param name="message">The message to display when the comparision fails</param>
        public static void AssertBitmapEquals(Bitmap bitmap1, Bitmap bitmap2, string message = "")
        {
            if(bitmap1.PixelFormat != bitmap2.PixelFormat)
                Assert.Fail(message);

            for (int y = 0; y < bitmap1.Height; y++)
            {
                for (int x = 0; x < bitmap1.Width; x++)
                {
                    Assert.AreEqual(bitmap1.GetPixel(x, y).ToArgb(), bitmap2.GetPixel(x, y).ToArgb(), message);
                }
            }
        }

        /// <summary>
        /// Asserts that the result of a copy region operation was successfull by analysing the source and target regions for pixel-by-pixel equalities
        /// </summary>
        /// <param name="canvasBitmap">The bitmap that was drawn into</param>
        /// <param name="copyBitmap">The bitmap that was copied from</param>
        /// <param name="targetRectangle">The region on the canvas bitmap that was drawn into</param>
        /// <param name="sourceRectangle">The region from the source rectangle that was drawn</param>
        /// <param name="message">The message to display on assertion error</param>
        public static void AssertCopyRegionEquals(Bitmap canvasBitmap, Bitmap copyBitmap, Rectangle targetRectangle,
            Rectangle sourceRectangle, string message = "Pixels of the target region must fully match the pixels from the origin region")
        {
            Rectangle srcBitmapRect = new Rectangle(0, 0, copyBitmap.Width, copyBitmap.Height);
            Rectangle destBitmapRect = new Rectangle(0, 0, canvasBitmap.Width, canvasBitmap.Height);

            // Check if the rectangle configuration doesn't generate invalid states or does not affect the target image
            if (sourceRectangle.Width <= 0 || sourceRectangle.Height <= 0 || targetRectangle.Width <= 0 || targetRectangle.Height <= 0 ||
                !srcBitmapRect.IntersectsWith(sourceRectangle) || !targetRectangle.IntersectsWith(destBitmapRect))
                return;

            // Find the areas of the first and second bitmaps that are going to be affected
            srcBitmapRect = Rectangle.Intersect(sourceRectangle, srcBitmapRect);

            // Clip the source rectangle on top of the destination rectangle in a way that clips out the regions of the original bitmap
            // that will not be drawn on the destination bitmap for being out of bounds
            srcBitmapRect = Rectangle.Intersect(srcBitmapRect, new Rectangle(sourceRectangle.X, sourceRectangle.Y, targetRectangle.Width, targetRectangle.Height));

            destBitmapRect = Rectangle.Intersect(targetRectangle, destBitmapRect);

            // Clipt the source bitmap region yet again here, this time against the available canvas bitmap rectangle
            // We transpose the second rectangle by the source's X and Y because we want to clip the target rectangle in the source rectangle's coordinates
            srcBitmapRect = Rectangle.Intersect(srcBitmapRect, new Rectangle(-targetRectangle.X + sourceRectangle.X, -targetRectangle.Y + sourceRectangle.Y, canvasBitmap.Width, canvasBitmap.Height));

            // Calculate the rectangle containing the maximum possible area that is supposed to be affected by the copy region operation
            int copyWidth = Math.Min(srcBitmapRect.Width, destBitmapRect.Width);
            int copyHeight = Math.Min(srcBitmapRect.Height, destBitmapRect.Height);

            if (copyWidth == 0 || copyHeight == 0)
                return;

            int srcStartX = srcBitmapRect.Left;
            int srcStartY = srcBitmapRect.Top;

            int destStartX = destBitmapRect.Left;
            int destStartY = destBitmapRect.Top;

            for (int y = 0; y < copyHeight; y++)
            {
                for (int x = 0; x < copyWidth; x++)
                {
                    int destX = destStartX;
                    int destY = destStartY + y;

                    int srcX = srcStartX;
                    int srcY = srcStartY + y;

                    Assert.AreEqual(copyBitmap.GetPixel(srcX, srcY).ToArgb(), canvasBitmap.GetPixel(destX, destY).ToArgb(), message);
                }
            }
        }

        /// <summary>
        /// Random number generator used to randomize seeds for image generation when none are provided
        /// </summary>
        private static readonly Random _seedRandom = new Random();
    }
}
