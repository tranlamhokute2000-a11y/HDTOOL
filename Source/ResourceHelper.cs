using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DashStyle = System.Drawing.Drawing2D.DashStyle;
using LinearGradientBrush = System.Drawing.Drawing2D.LinearGradientBrush;
using Pen = System.Drawing.Pen;
using Brush = System.Drawing.Brush;
using SolidBrush = System.Drawing.SolidBrush;
using Font = System.Drawing.Font;
using Color = System.Drawing.Color;

namespace HDTool
{
    /// <summary>
    /// Hỗ trợ tạo icon đồ họa cho Ribbon Revit
    /// </summary>
    public static class ResourceHelper
    {
        public static ImageSource CreateLevelCropIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // Khung Crop View hình chữ nhật nét đứt
                    using (System.Drawing.Pen cropPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(148, 163, 184), 1.2f * scale))
                    {
                        cropPen.DashStyle = DashStyle.Dash;
                        g.DrawRectangle(cropPen, 8f * scale, 4f * scale, 16f * scale, 24f * scale);
                    }

                    // Đường Level 2 (Tầng trên)
                    using (System.Drawing.Pen levelPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(220, 38, 38), 1.8f * scale))
                    {
                        levelPen.DashStyle = DashStyle.DashDot;
                        g.DrawLine(levelPen, 2f * scale, 10f * scale, 29f * scale, 10f * scale);
                    }
                    // Đầu tam giác Level Head trên
                    using (System.Drawing.SolidBrush lvlBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(220, 38, 38)))
                    {
                        PointF[] tri = new PointF[] {
                            new PointF(2f * scale, 10f * scale),
                            new PointF(7f * scale, 6.5f * scale),
                            new PointF(7f * scale, 13.5f * scale)
                        };
                        g.FillPolygon(lvlBrush, tri);
                    }

                    // Đường Level 1 (Tầng dưới)
                    using (System.Drawing.Pen levelPen2 = new System.Drawing.Pen(System.Drawing.Color.FromArgb(2, 132, 199), 1.8f * scale))
                    {
                        levelPen2.DashStyle = DashStyle.DashDot;
                        g.DrawLine(levelPen2, 2f * scale, 22f * scale, 29f * scale, 22f * scale);
                    }
                    // Đầu tam giác Level Head dưới
                    using (System.Drawing.SolidBrush lvlBrush2 = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(2, 132, 199)))
                    {
                        PointF[] tri2 = new PointF[] {
                            new PointF(2f * scale, 22f * scale),
                            new PointF(7f * scale, 18.5f * scale),
                            new PointF(7f * scale, 25.5f * scale)
                        };
                        g.FillPolygon(lvlBrush2, tri2);
                    }

                    // Mũi tên cam đo căn chỉnh khoảng cách
                    using (System.Drawing.Pen arrowPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(234, 88, 12), 1.3f * scale))
                    {
                        g.DrawLine(arrowPen, 24f * scale, 10f * scale, 24f * scale, 22f * scale);
                        g.DrawLine(arrowPen, 22f * scale, 10f * scale, 26f * scale, 10f * scale);
                        g.DrawLine(arrowPen, 22f * scale, 22f * scale, 26f * scale, 22f * scale);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        public static ImageSource CreateGridCropIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // Khung Crop View hình chữ nhật nét đứt
                    using (System.Drawing.Pen cropPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(148, 163, 184), 1.2f * scale))
                    {
                        cropPen.DashStyle = DashStyle.Dash;
                        g.DrawRectangle(cropPen, 8f * scale, 8f * scale, 16f * scale, 16f * scale);
                    }

                    // Đường Grid Dọc (Trục 1)
                    using (System.Drawing.Pen gridPenV = new System.Drawing.Pen(System.Drawing.Color.FromArgb(2, 132, 199), 1.6f * scale))
                    {
                        g.DrawLine(gridPenV, 16f * scale, 3f * scale, 16f * scale, 29f * scale);
                    }

                    // Bubble Tròn đỉnh trục dọc (chữ "1")
                    using (System.Drawing.SolidBrush bBg = new System.Drawing.SolidBrush(System.Drawing.Color.White))
                    using (System.Drawing.Pen bBorder = new System.Drawing.Pen(System.Drawing.Color.FromArgb(2, 132, 199), 1.4f * scale))
                    {
                        g.FillEllipse(bBg, 12.5f * scale, 1f * scale, 7f * scale, 7f * scale);
                        g.DrawEllipse(bBorder, 12.5f * scale, 1f * scale, 7f * scale, 7f * scale);
                        if (size >= 32)
                        {
                            using (System.Drawing.Font font = new System.Drawing.Font("Segoe UI", 4.5f, System.Drawing.FontStyle.Bold))
                            using (System.Drawing.SolidBrush textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(2, 132, 199)))
                            using (System.Drawing.StringFormat sf = new System.Drawing.StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                            {
                                g.DrawString("1", font, textBrush, new RectangleF(12.5f * scale, 1f * scale, 7f * scale, 7f * scale), sf);
                            }
                        }
                    }

                    // Đường Grid Ngang (Trục A)
                    using (System.Drawing.Pen gridPenH = new System.Drawing.Pen(System.Drawing.Color.FromArgb(16, 185, 129), 1.6f * scale))
                    {
                        g.DrawLine(gridPenH, 3f * scale, 16f * scale, 29f * scale, 16f * scale);
                    }

                    // Bubble Tròn bên trái trục ngang (chữ "A")
                    using (System.Drawing.SolidBrush bBg = new System.Drawing.SolidBrush(System.Drawing.Color.White))
                    using (System.Drawing.Pen bBorder = new System.Drawing.Pen(System.Drawing.Color.FromArgb(16, 185, 129), 1.4f * scale))
                    {
                        g.FillEllipse(bBg, 1f * scale, 12.5f * scale, 7f * scale, 7f * scale);
                        g.DrawEllipse(bBorder, 1f * scale, 12.5f * scale, 7f * scale, 7f * scale);
                        if (size >= 32)
                        {
                            using (System.Drawing.Font font = new System.Drawing.Font("Segoe UI", 4.5f, System.Drawing.FontStyle.Bold))
                            using (System.Drawing.SolidBrush textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(16, 185, 129)))
                            using (System.Drawing.StringFormat sf = new System.Drawing.StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                            {
                                g.DrawString("A", font, textBrush, new RectangleF(1f * scale, 12.5f * scale, 7f * scale, 7f * scale), sf);
                            }
                        }
                    }

                    // Mũi tên đo căn chỉnh
                    using (System.Drawing.Pen arrowPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(234, 88, 12), 1.3f * scale))
                    {
                        g.DrawLine(arrowPen, 21f * scale, 16f * scale, 26f * scale, 16f * scale);
                        g.DrawLine(arrowPen, 16f * scale, 21f * scale, 16f * scale, 26f * scale);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        public static ImageSource CreateSmartIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // Vẽ ngôi sao ma thuật hoặc biểu tượng Smart
                    using (System.Drawing.SolidBrush brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(241, 196, 15)))
                    {
                        PointF[] star = new PointF[] {
                            new PointF(16f * scale, 3f * scale),
                            new PointF(20f * scale, 12f * scale),
                            new PointF(29f * scale, 13f * scale),
                            new PointF(22f * scale, 19f * scale),
                            new PointF(24f * scale, 28f * scale),
                            new PointF(16f * scale, 23f * scale),
                            new PointF(8f * scale, 28f * scale),
                            new PointF(10f * scale, 19f * scale),
                            new PointF(3f * scale, 13f * scale),
                            new PointF(12f * scale, 12f * scale)
                        };
                        g.FillPolygon(brush, star);
                    }

                    using (System.Drawing.Pen border = new System.Drawing.Pen(System.Drawing.Color.FromArgb(211, 84, 0), 1.2f * scale))
                    {
                        PointF[] star = new PointF[] {
                            new PointF(16f * scale, 3f * scale),
                            new PointF(20f * scale, 12f * scale),
                            new PointF(29f * scale, 13f * scale),
                            new PointF(22f * scale, 19f * scale),
                            new PointF(24f * scale, 28f * scale),
                            new PointF(16f * scale, 23f * scale),
                            new PointF(8f * scale, 28f * scale),
                            new PointF(10f * scale, 19f * scale),
                            new PointF(3f * scale, 13f * scale),
                            new PointF(12f * scale, 12f * scale)
                        };
                        g.DrawPolygon(border, star);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        public static ImageSource CreateHDToolMenuIcon(int size) { return CreateHDToolIcon(size); }
        public static ImageSource CreateHDToolIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // Vẽ badge nền bo tròn màu xanh hải quân đậm hiện đại (#182B49)
                    using (GraphicsPath path = new GraphicsPath())
                    {
                        float rad = 4f * scale;
                        float w = 30f * scale;
                        float h = 30f * scale;
                        float x = 1f * scale;
                        float y = 1f * scale;
                        path.AddArc(x, y, rad, rad, 180, 90);
                        path.AddArc(x + w - rad, y, rad, rad, 270, 90);
                        path.AddArc(x + w - rad, y + h - rad, rad, rad, 0, 90);
                        path.AddArc(x, y + h - rad, rad, rad, 90, 90);
                        path.CloseFigure();

                        using (System.Drawing.Drawing2D.LinearGradientBrush lgb = new System.Drawing.Drawing2D.LinearGradientBrush(
                            new System.Drawing.PointF(0, 0), new System.Drawing.PointF(0, size),
                            System.Drawing.Color.FromArgb(24, 43, 73), System.Drawing.Color.FromArgb(15, 30, 54)))
                        {
                            g.FillPath(lgb, path);
                        }

                        using (System.Drawing.Pen borderPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(52, 152, 219), 1.2f * scale))
                        {
                            g.DrawPath(borderPen, path);
                        }
                    }

                    // Chữ HD màu trắng nổi bật
                    using (System.Drawing.Font font = new System.Drawing.Font("Segoe UI", (size >= 32) ? 10.5f : 6.0f, System.Drawing.FontStyle.Bold))
                    using (System.Drawing.SolidBrush textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White))
                    using (System.Drawing.StringFormat sf = new System.Drawing.StringFormat())
                    {
                        sf.Alignment = System.Drawing.StringAlignment.Center;
                        sf.LineAlignment = System.Drawing.StringAlignment.Center;
                        if (size >= 32)
                        {
                            g.DrawString("HD", font, textBrush, new System.Drawing.RectangleF(0, 2f * scale, size, 16f * scale), sf);
                        }
                        else
                        {
                            g.DrawString("HD", font, textBrush, new System.Drawing.RectangleF(0, 0, size, size), sf);
                        }
                    }

                    if (size >= 32)
                    {
                        // Nhãn phụ màu cam vàng TOOL
                        using (System.Drawing.Font fontSmall = new System.Drawing.Font("Segoe UI", 5.5f, System.Drawing.FontStyle.Bold))
                        using (System.Drawing.SolidBrush goldBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(243, 156, 18)))
                        using (System.Drawing.StringFormat sf = new System.Drawing.StringFormat())
                        {
                            sf.Alignment = System.Drawing.StringAlignment.Center;
                            sf.LineAlignment = System.Drawing.StringAlignment.Center;
                            g.DrawString("TOOL", fontSmall, goldBrush, new System.Drawing.RectangleF(0, 17f * scale, size, 12f * scale), sf);
                        }
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        public static ImageSource CreateSheetListIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // Vẽ tờ giấy / tài liệu trắng có bóng mờ
                    using (System.Drawing.SolidBrush shadowBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(40, 0, 0, 0)))
                    {
                        g.FillRectangle(shadowBrush, 5f * scale + 1.5f, 3f * scale + 1.5f, 22f * scale, 26f * scale);
                    }

                    using (System.Drawing.SolidBrush paperBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White))
                    using (System.Drawing.Pen paperBorder = new System.Drawing.Pen(System.Drawing.Color.FromArgb(149, 165, 166), 1.2f * scale))
                    {
                        g.FillRectangle(paperBrush, 5f * scale, 3f * scale, 22f * scale, 26f * scale);
                        g.DrawRectangle(paperBorder, 5f * scale, 3f * scale, 22f * scale, 26f * scale);
                    }

                    // Header bar của tài liệu màu xanh hải quân HDTOOL (#182B49)
                    using (System.Drawing.SolidBrush headerBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(24, 43, 73)))
                    {
                        g.FillRectangle(headerBrush, 5f * scale, 3f * scale, 22f * scale, 7f * scale);
                    }

                    // Các dòng danh mục bản vẽ (kẻ bảng)
                    using (System.Drawing.Pen linePen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(189, 195, 199), 1.0f * scale))
                    using (System.Drawing.Pen highlightPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(41, 128, 185), 1.5f * scale))
                    {
                        // Dòng 1: Số hiệu & tên
                        g.DrawLine(highlightPen, 8f * scale, 13f * scale, 13f * scale, 13f * scale);
                        g.DrawLine(linePen, 15f * scale, 13f * scale, 24f * scale, 13f * scale);

                        // Dòng 2
                        g.DrawLine(highlightPen, 8f * scale, 17f * scale, 13f * scale, 17f * scale);
                        g.DrawLine(linePen, 15f * scale, 17f * scale, 24f * scale, 17f * scale);

                        // Dòng 3
                        g.DrawLine(highlightPen, 8f * scale, 21f * scale, 13f * scale, 21f * scale);
                        g.DrawLine(linePen, 15f * scale, 21f * scale, 24f * scale, 21f * scale);
                    }

                    // Huy hiệu nhỏ màu xanh lá Excel ở góc dưới phải (Biểu tượng xuất Excel)
                    using (System.Drawing.SolidBrush excelBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(39, 174, 96)))
                    using (System.Drawing.Pen excelBorder = new System.Drawing.Pen(System.Drawing.Color.White, 1.2f * scale))
                    {
                        g.FillRectangle(excelBrush, 17f * scale, 18f * scale, 12f * scale, 11f * scale);
                        g.DrawRectangle(excelBorder, 17f * scale, 18f * scale, 12f * scale, 11f * scale);

                        if (size >= 32)
                        {
                            using (System.Drawing.Font fontX = new System.Drawing.Font("Segoe UI", 6.5f, System.Drawing.FontStyle.Bold))
                            using (System.Drawing.SolidBrush whiteBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White))
                            using (System.Drawing.StringFormat sf = new System.Drawing.StringFormat())
                            {
                                sf.Alignment = System.Drawing.StringAlignment.Center;
                                sf.LineAlignment = System.Drawing.StringAlignment.Center;
                                g.DrawString("X", fontX, whiteBrush, new System.Drawing.RectangleF(17f * scale, 18f * scale, 12f * scale, 11f * scale), sf);
                            }
                        }
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        /// <summary>
        /// Tạo icon Ribbon cho lệnh Cập Nhật Danh Mục Bản Vẽ từ Excel (với huy hiệu đồng bộ màu cam)
        /// </summary>
        public static ImageSource CreateUpdateSheetListIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // Vẽ tờ bản vẽ nền trắng viền xanh
                    using (System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath())
                    {
                        path.AddLine(5f * scale, 3f * scale, 21f * scale, 3f * scale);
                        path.AddLine(21f * scale, 3f * scale, 27f * scale, 9f * scale);
                        path.AddLine(27f * scale, 9f * scale, 27f * scale, 29f * scale);
                        path.AddLine(27f * scale, 29f * scale, 5f * scale, 29f * scale);
                        path.CloseFigure();

                        using (System.Drawing.SolidBrush sheetBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White))
                        {
                            g.FillPath(sheetBrush, path);
                        }

                        using (System.Drawing.Pen borderPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(24, 43, 73), 1.6f * scale))
                        {
                            g.DrawPath(borderPen, path);
                        }
                    }

                    // Góc gập tờ giấy
                    using (System.Drawing.Drawing2D.GraphicsPath foldPath = new System.Drawing.Drawing2D.GraphicsPath())
                    {
                        foldPath.AddLine(21f * scale, 3f * scale, 21f * scale, 9f * scale);
                        foldPath.AddLine(21f * scale, 9f * scale, 27f * scale, 9f * scale);
                        using (System.Drawing.SolidBrush foldBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(220, 224, 230)))
                        {
                            g.FillPath(foldBrush, foldPath);
                        }
                        using (System.Drawing.Pen foldPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(24, 43, 73), 1.2f * scale))
                        {
                            g.DrawPath(foldPen, foldPath);
                        }
                    }

                    // Các dòng danh mục bản vẽ
                    using (System.Drawing.Pen linePen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(189, 195, 199), 1.0f * scale))
                    using (System.Drawing.Pen highlightPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(211, 84, 0), 1.5f * scale))
                    {
                        g.DrawLine(highlightPen, 8f * scale, 13f * scale, 13f * scale, 13f * scale);
                        g.DrawLine(linePen, 15f * scale, 13f * scale, 24f * scale, 13f * scale);

                        g.DrawLine(highlightPen, 8f * scale, 17f * scale, 13f * scale, 17f * scale);
                        g.DrawLine(linePen, 15f * scale, 17f * scale, 24f * scale, 17f * scale);

                        g.DrawLine(highlightPen, 8f * scale, 21f * scale, 13f * scale, 21f * scale);
                        g.DrawLine(linePen, 15f * scale, 21f * scale, 24f * scale, 21f * scale);
                    }

                    // Huy hiệu hình tròn màu cam biểu tượng làm mới / đồng bộ ở góc dưới phải
                    using (System.Drawing.SolidBrush badgeBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(211, 84, 0)))
                    using (System.Drawing.Pen badgeBorder = new System.Drawing.Pen(System.Drawing.Color.White, 1.2f * scale))
                    {
                        g.FillEllipse(badgeBrush, 16f * scale, 16f * scale, 14f * scale, 14f * scale);
                        g.DrawEllipse(badgeBorder, 16f * scale, 16f * scale, 14f * scale, 14f * scale);

                        if (size >= 32)
                        {
                            using (System.Drawing.Font fontArrow = new System.Drawing.Font("Segoe UI", 7.0f, System.Drawing.FontStyle.Bold))
                            using (System.Drawing.SolidBrush whiteBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White))
                            using (System.Drawing.StringFormat sf = new System.Drawing.StringFormat())
                            {
                                sf.Alignment = System.Drawing.StringAlignment.Center;
                                sf.LineAlignment = System.Drawing.StringAlignment.Center;
                                g.DrawString("⟳", fontArrow, whiteBrush, new System.Drawing.RectangleF(16f * scale, 16f * scale, 14f * scale, 14f * scale), sf);
                            }
                        }
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        public static ImageSource CreateUpdateToolIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // Vẽ vòng tròn nền xanh ngọc đậm (Emerald Green / Teal)
                    using (System.Drawing.SolidBrush bgBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(22, 160, 133)))
                    {
                        g.FillEllipse(bgBrush, 2f * scale, 2f * scale, 28f * scale, 28f * scale);
                    }

                    // Vòng tròn cung tên xoay (Reload Arc)
                    using (System.Drawing.Pen arcPen = new System.Drawing.Pen(System.Drawing.Color.White, 2.5f * scale))
                    {
                        arcPen.StartCap = LineCap.Round;
                        arcPen.EndCap = LineCap.Round;
                        g.DrawArc(arcPen, 6f * scale, 6f * scale, 20f * scale, 20f * scale, 45, 270);
                    }

                    // Mũi tên của vòng xoay
                    using (System.Drawing.SolidBrush arrowBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White))
                    {
                        System.Drawing.PointF[] arrowPts = new System.Drawing.PointF[]
                        {
                            new System.Drawing.PointF(24f * scale, 12f * scale),
                            new System.Drawing.PointF(28f * scale, 17f * scale),
                            new System.Drawing.PointF(20f * scale, 17f * scale)
                        };
                        g.FillPolygon(arrowBrush, arrowPts);
                    }

                    // Biểu tượng tia chớp màu vàng gold ở trung tâm
                    using (System.Drawing.SolidBrush boltBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(241, 196, 15)))
                    {
                        System.Drawing.PointF[] boltPts = new System.Drawing.PointF[]
                        {
                            new System.Drawing.PointF(17f * scale, 8f * scale),
                            new System.Drawing.PointF(11f * scale, 17f * scale),
                            new System.Drawing.PointF(16f * scale, 17f * scale),
                            new System.Drawing.PointF(14f * scale, 24f * scale),
                            new System.Drawing.PointF(21f * scale, 15f * scale),
                            new System.Drawing.PointF(16f * scale, 15f * scale)
                        };
                        g.FillPolygon(boltBrush, boltPts);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        public static ImageSource CreateCropAlignCombinedIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // 1. Khung Crop View hình chữ nhật nét đứt màu xám xanh
                    using (System.Drawing.Pen cropPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(148, 163, 184), 1.2f * scale))
                    {
                        cropPen.DashStyle = DashStyle.Dash;
                        g.DrawRectangle(cropPen, 8f * scale, 8f * scale, 16f * scale, 16f * scale);
                    }

                    // 2. Đường Trục Grid 1 (Trục dọc bên trái)
                    using (System.Drawing.Pen gridPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(2, 132, 199), 1.6f * scale))
                    {
                        g.DrawLine(gridPen, 11f * scale, 3f * scale, 11f * scale, 28f * scale);
                    }

                    // Bubble Grid Tròn đỉnh trục 1 (chữ "1")
                    using (System.Drawing.SolidBrush bBg = new System.Drawing.SolidBrush(System.Drawing.Color.White))
                    using (System.Drawing.Pen bBorder = new System.Drawing.Pen(System.Drawing.Color.FromArgb(2, 132, 199), 1.4f * scale))
                    {
                        g.FillEllipse(bBg, 7.5f * scale, 1f * scale, 7f * scale, 7f * scale);
                        g.DrawEllipse(bBorder, 7.5f * scale, 1f * scale, 7f * scale, 7f * scale);
                        if (size >= 32)
                        {
                            using (System.Drawing.Font font = new System.Drawing.Font("Segoe UI", 4.5f, System.Drawing.FontStyle.Bold))
                            using (System.Drawing.SolidBrush textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(2, 132, 199)))
                            using (System.Drawing.StringFormat sf = new System.Drawing.StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                            {
                                g.DrawString("1", font, textBrush, new RectangleF(7.5f * scale, 1f * scale, 7f * scale, 7f * scale), sf);
                            }
                        }
                    }

                    // 3. Đường Trục Grid 2 (Trục dọc bên phải)
                    using (System.Drawing.Pen gridPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(2, 132, 199), 1.6f * scale))
                    {
                        g.DrawLine(gridPen, 21f * scale, 3f * scale, 21f * scale, 28f * scale);
                    }

                    // Bubble Grid Tròn đỉnh trục 2 (chữ "2")
                    using (System.Drawing.SolidBrush bBg = new System.Drawing.SolidBrush(System.Drawing.Color.White))
                    using (System.Drawing.Pen bBorder = new System.Drawing.Pen(System.Drawing.Color.FromArgb(2, 132, 199), 1.4f * scale))
                    {
                        g.FillEllipse(bBg, 17.5f * scale, 1f * scale, 7f * scale, 7f * scale);
                        g.DrawEllipse(bBorder, 17.5f * scale, 1f * scale, 7f * scale, 7f * scale);
                        if (size >= 32)
                        {
                            using (System.Drawing.Font font = new System.Drawing.Font("Segoe UI", 4.5f, System.Drawing.FontStyle.Bold))
                            using (System.Drawing.SolidBrush textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(2, 132, 199)))
                            using (System.Drawing.StringFormat sf = new System.Drawing.StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                            {
                                g.DrawString("2", font, textBrush, new RectangleF(17.5f * scale, 1f * scale, 7f * scale, 7f * scale), sf);
                            }
                        }
                    }

                    // 4. Đường Cao Độ Level (Ngang)
                    using (System.Drawing.Pen levelPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(220, 38, 38), 1.8f * scale))
                    {
                        levelPen.DashStyle = DashStyle.DashDot;
                        g.DrawLine(levelPen, 3f * scale, 21f * scale, 29f * scale, 21f * scale);
                    }

                    // Ký hiệu đầu Level (Tam giác cao độ Level Head chuẩn kiến trúc)
                    using (System.Drawing.SolidBrush lvlBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(220, 38, 38)))
                    {
                        PointF[] tri = new PointF[] {
                            new PointF(2f * scale, 21f * scale),
                            new PointF(7f * scale, 17.5f * scale),
                            new PointF(7f * scale, 24.5f * scale)
                        };
                        g.FillPolygon(lvlBrush, tri);
                    }

                    // 5. Mũi tên đo căn chỉnh 2 chiều màu cam sắc nét
                    using (System.Drawing.Pen arrowPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(234, 88, 12), 1.4f * scale))
                    {
                        // Đo ngang giữa 2 trục Grid
                        g.DrawLine(arrowPen, 11f * scale, 13f * scale, 21f * scale, 13f * scale);
                        g.DrawLine(arrowPen, 11f * scale, 11f * scale, 11f * scale, 15f * scale);
                        g.DrawLine(arrowPen, 21f * scale, 11f * scale, 21f * scale, 15f * scale);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        /// <summary>
        /// Tạo icon Ribbon cho lệnh Thống Kê Khối Lượng Ống Nước & Phụ Kiện (Biểu tượng ống nước MEP chuẩn 100%)
        /// </summary>
        public static ImageSource CreateMepQuantityIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // 1. Vẽ Đoạn Ống Nước Nằm Ngang (Màu Xanh Dương Cấp Thoát Nước #0284c7)
                    using (LinearGradientBrush pipeHGrad = new LinearGradientBrush(
                        new PointF(0, 16f * scale), new PointF(0, 26f * scale),
                        System.Drawing.Color.FromArgb(56, 189, 248), System.Drawing.Color.FromArgb(3, 105, 161)))
                    {
                        g.FillRectangle(pipeHGrad, 1f * scale, 17f * scale, 18f * scale, 8f * scale);
                    }
                    using (System.Drawing.Pen pipeBorder = new System.Drawing.Pen(System.Drawing.Color.FromArgb(7, 89, 133), 1.2f * scale))
                    {
                        g.DrawRectangle(pipeBorder, 1f * scale, 17f * scale, 18f * scale, 8f * scale);
                    }

                    // Gờ nối / Bích ống bên trái
                    using (System.Drawing.SolidBrush flangeBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(2, 132, 199)))
                    using (System.Drawing.Pen flangeBorder = new System.Drawing.Pen(System.Drawing.Color.FromArgb(7, 89, 133), 1.2f * scale))
                    {
                        g.FillRectangle(flangeBrush, 1f * scale, 15.5f * scale, 3.5f * scale, 11f * scale);
                        g.DrawRectangle(flangeBorder, 1f * scale, 15.5f * scale, 3.5f * scale, 11f * scale);
                    }

                    // 2. Vẽ Co Ống 90 Độ (Elbow) chuyển hướng đi lên
                    using (GraphicsPath elbowPath = new GraphicsPath())
                    {
                        elbowPath.AddArc(11f * scale, 11f * scale, 14f * scale, 14f * scale, 0, 90);
                        elbowPath.AddLine(18f * scale, 25f * scale, 18f * scale, 17f * scale);
                        elbowPath.AddArc(11f * scale, 11f * scale, 6f * scale, 6f * scale, 90, -90);
                        elbowPath.CloseFigure();
                        using (LinearGradientBrush elbowGrad = new LinearGradientBrush(
                            new PointF(11f * scale, 11f * scale), new PointF(25f * scale, 25f * scale),
                            System.Drawing.Color.FromArgb(56, 189, 248), System.Drawing.Color.FromArgb(3, 105, 161)))
                        {
                            g.FillPath(elbowGrad, elbowPath);
                        }
                        using (System.Drawing.Pen borderPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(7, 89, 133), 1.2f * scale))
                        {
                            g.DrawPath(borderPen, elbowPath);
                        }
                    }

                    // 3. Đoạn Ống Đứng Đi Lên
                    using (LinearGradientBrush pipeVGrad = new LinearGradientBrush(
                        new PointF(17f * scale, 0), new PointF(25f * scale, 0),
                        System.Drawing.Color.FromArgb(56, 189, 248), System.Drawing.Color.FromArgb(3, 105, 161)))
                    {
                        g.FillRectangle(pipeVGrad, 17f * scale, 3f * scale, 8f * scale, 14f * scale);
                    }
                    using (System.Drawing.Pen pipeBorder = new System.Drawing.Pen(System.Drawing.Color.FromArgb(7, 89, 133), 1.2f * scale))
                    {
                        g.DrawRectangle(pipeBorder, 17f * scale, 3f * scale, 8f * scale, 14f * scale);
                    }

                    // Gờ nối / Bích ống ở đầu trên
                    using (System.Drawing.SolidBrush flangeBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(2, 132, 199)))
                    using (System.Drawing.Pen flangeBorder = new System.Drawing.Pen(System.Drawing.Color.FromArgb(7, 89, 133), 1.2f * scale))
                    {
                        g.FillRectangle(flangeBrush, 15.5f * scale, 2f * scale, 11f * scale, 3.5f * scale);
                        g.DrawRectangle(flangeBorder, 15.5f * scale, 2f * scale, 11f * scale, 3.5f * scale);
                    }

                    // 4. Giọt Nước Xanh Ngọc Long Lanh (Water Drop) rực rỡ ở trung tâm
                    using (GraphicsPath dropPath = new GraphicsPath())
                    {
                        dropPath.AddLine(8.5f * scale, 3f * scale, 12f * scale, 9f * scale);
                        dropPath.AddArc(5f * scale, 6.5f * scale, 7f * scale, 7f * scale, 30, 200);
                        dropPath.CloseFigure();

                        using (LinearGradientBrush dropGrad = new LinearGradientBrush(
                            new PointF(5f * scale, 3f * scale), new PointF(12f * scale, 14f * scale),
                            System.Drawing.Color.FromArgb(56, 189, 248), System.Drawing.Color.FromArgb(14, 165, 233)))
                        {
                            g.FillPath(dropGrad, dropPath);
                        }
                        using (System.Drawing.Pen dropBorder = new System.Drawing.Pen(System.Drawing.Color.White, 1.0f * scale))
                        {
                            g.DrawPath(dropBorder, dropPath);
                        }
                        // Điểm sáng phản chiếu trên giọt nước
                        using (System.Drawing.SolidBrush hlBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(200, 255, 255, 255)))
                        {
                            g.FillEllipse(hlBrush, 7f * scale, 7f * scale, 2f * scale, 3.5f * scale);
                        }
                    }

                    // 5. Huy Hiệu Bảng Thống Kê Nhỏ (#10b981) ở góc dưới phải thể hiện chức năng thống kê khối lượng
                    using (System.Drawing.SolidBrush bgBadge = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(16, 185, 129)))
                    using (System.Drawing.Pen borderBadge = new System.Drawing.Pen(System.Drawing.Color.White, 1.2f * scale))
                    {
                        g.FillRectangle(bgBadge, 17f * scale, 17f * scale, 14f * scale, 13f * scale);
                        g.DrawRectangle(borderBadge, 17f * scale, 17f * scale, 14f * scale, 13f * scale);
                        using (System.Drawing.Pen lineP = new System.Drawing.Pen(System.Drawing.Color.White, 1.0f * scale))
                        {
                            g.DrawLine(lineP, 19f * scale, 21f * scale, 29f * scale, 21f * scale);
                            g.DrawLine(lineP, 19f * scale, 25f * scale, 27f * scale, 25f * scale);
                        }
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        /// <summary>
        /// Tạo icon Ribbon chuyên biệt cho lệnh Soi Sáng Bảng Khối Lượng Trực Tiếp Trên Sheet:
        /// Tờ bản vẽ Sheet kẻ bảng tính + Kính lúp soi chiếu dòng bảng phát tia sáng rực rỡ
        /// </summary>
        public static ImageSource CreatePickTableHighlightIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // 1. Bản vẽ Sheet nền trắng
                    using (System.Drawing.SolidBrush paperBg = new System.Drawing.SolidBrush(System.Drawing.Color.White))
                    using (System.Drawing.Pen paperBorder = new System.Drawing.Pen(System.Drawing.Color.FromArgb(100, 116, 139), 1.2f * scale))
                    {
                        g.FillRectangle(paperBg, 2f * scale, 3f * scale, 20f * scale, 26f * scale);
                        g.DrawRectangle(paperBorder, 2f * scale, 3f * scale, 20f * scale, 26f * scale);
                    }

                    // Header bảng Sheet màu xanh tím
                    using (System.Drawing.SolidBrush headerBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(30, 41, 59)))
                    {
                        g.FillRectangle(headerBrush, 2f * scale, 3f * scale, 20f * scale, 6f * scale);
                    }

                    // Các dòng trong bảng Sheet
                    using (System.Drawing.Pen linePen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(203, 213, 225), 1.0f * scale))
                    {
                        g.DrawLine(linePen, 4f * scale, 13f * scale, 20f * scale, 13f * scale);
                        g.DrawLine(linePen, 4f * scale, 17f * scale, 20f * scale, 17f * scale);
                        g.DrawLine(linePen, 4f * scale, 21f * scale, 20f * scale, 21f * scale);
                        g.DrawLine(linePen, 4f * scale, 25f * scale, 20f * scale, 25f * scale);
                    }

                    // Dòng đang được SOI SÁNG (màu đỏ rực rỡ nổi bật)
                    using (System.Drawing.SolidBrush hlRow = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(239, 68, 68)))
                    {
                        g.FillRectangle(hlRow, 3f * scale, 14f * scale, 18f * scale, 3f * scale);
                    }

                    // 2. Kính Lúp Soi Sáng (Spotlight Magnifier)
                    // Cán kính lúp
                    using (System.Drawing.Pen handlePen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(51, 65, 85), 3.5f * scale))
                    {
                        handlePen.StartCap = LineCap.Round;
                        handlePen.EndCap = LineCap.Round;
                        g.DrawLine(handlePen, 24f * scale, 24f * scale, 30f * scale, 30f * scale);
                    }

                    // Tròng kính lúp tròn viền vàng cam sáng bóng
                    using (System.Drawing.SolidBrush lensBg = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(160, 254, 243, 199)))
                    using (System.Drawing.Pen lensRim = new System.Drawing.Pen(System.Drawing.Color.FromArgb(217, 119, 6), 2.2f * scale))
                    {
                        g.FillEllipse(lensBg, 13f * scale, 10f * scale, 14f * scale, 14f * scale);
                        g.DrawEllipse(lensRim, 13f * scale, 10f * scale, 14f * scale, 14f * scale);
                    }

                    // Tia sáng lấp lánh (Sparkle) phát sáng từ kính lúp
                    using (System.Drawing.SolidBrush sparkBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(234, 88, 12)))
                    {
                        PointF[] spark = new PointF[] {
                            new PointF(20f * scale, 7f * scale),
                            new PointF(21f * scale, 10f * scale),
                            new PointF(24f * scale, 11f * scale),
                            new PointF(21f * scale, 12f * scale),
                            new PointF(20f * scale, 15f * scale),
                            new PointF(19f * scale, 12f * scale),
                            new PointF(16f * scale, 11f * scale),
                            new PointF(19f * scale, 10f * scale)
                        };
                        g.FillPolygon(sparkBrush, spark);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        /// <summary>
        /// Tạo icon Ribbon cho lệnh Căn Khớp Tỷ Lệ Bảng Khối Lượng (Fit Scale & Table Grid)
        /// </summary>
        public static ImageSource CreateFitScaleIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // 1. Khung bảng màu xanh ngọc (Emerald Green)
                    using (System.Drawing.SolidBrush bgBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(236, 253, 245)))
                    using (System.Drawing.Pen borderPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(16, 185, 129), 1.5f * scale))
                    {
                        g.FillRectangle(bgBrush, 2f * scale, 3f * scale, 28f * scale, 26f * scale);
                        g.DrawRectangle(borderPen, 2f * scale, 3f * scale, 28f * scale, 26f * scale);
                    }

                    // 2. Header bảng màu xanh ngọc đậm (#047857)
                    using (System.Drawing.SolidBrush headBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(4, 120, 87)))
                    {
                        g.FillRectangle(headBrush, 2f * scale, 3f * scale, 28f * scale, 7.5f * scale);
                        if (size >= 32)
                        {
                            using (System.Drawing.Font font = new System.Drawing.Font("Segoe UI", 4.5f, System.Drawing.FontStyle.Bold))
                            using (System.Drawing.SolidBrush textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White))
                            using (System.Drawing.StringFormat sf = new System.Drawing.StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                            {
                                g.DrawString("TỶ LỆ 1:2", font, textBrush, new RectangleF(2f * scale, 3f * scale, 28f * scale, 7.5f * scale), sf);
                            }
                        }
                    }

                    // 3. Đường phân cách cột và hàng
                    using (System.Drawing.Pen gridPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(16, 185, 129), 1.0f * scale))
                    {
                        g.DrawLine(gridPen, 11f * scale, 10.5f * scale, 11f * scale, 29f * scale);
                        g.DrawLine(gridPen, 21f * scale, 10.5f * scale, 21f * scale, 29f * scale);
                        g.DrawLine(gridPen, 2f * scale, 19.5f * scale, 30f * scale, 19.5f * scale);
                    }

                    // 4. Mũi tên 2 chiều màu cam vàng đo căn chỉnh tỉ lệ (Scale Dimension)
                    using (System.Drawing.Pen arrowPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(245, 158, 11), 1.4f * scale))
                    {
                        g.DrawLine(arrowPen, 4f * scale, 24f * scale, 10f * scale, 24f * scale);
                    }

                    // 5. Huy hiệu dấu tích xanh biển chứng nhận khớp 100%
                    using (System.Drawing.SolidBrush badgeBg = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(2, 132, 199)))
                    using (System.Drawing.Pen badgeRim = new System.Drawing.Pen(System.Drawing.Color.White, 1.0f * scale))
                    {
                        g.FillEllipse(badgeBg, 16f * scale, 16f * scale, 11f * scale, 11f * scale);
                        g.DrawEllipse(badgeRim, 16f * scale, 16f * scale, 11f * scale, 11f * scale);
                        if (size >= 32)
                        {
                            using (System.Drawing.Font font = new System.Drawing.Font("Segoe UI", 6.0f, System.Drawing.FontStyle.Bold))
                            using (System.Drawing.SolidBrush textBrush = new System.Drawing.SolidBrush(System.Drawing.Color.White))
                            using (System.Drawing.StringFormat sf = new System.Drawing.StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                            {
                                g.DrawString("✓", font, textBrush, new RectangleF(16f * scale, 16f * scale, 11f * scale, 11f * scale), sf);
                            }
                        }
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        /// <summary>
        /// Tạo icon Ribbon chuyên biệt cho tính năng Khối Lượng Ống Gió HVAC (Tôn mạ kẽm m²):
        /// Ống gió 3D Isometric chuẩn cơ điện MEP, mặt bích TDC nẹp C, gân tăng cứng chéo,
        /// luồng gió xanh ngọc lướt qua và huy hiệu vàng m² diện tích ống.
        /// </summary>
        public static ImageSource CreateDuctQuantityIcon(int size)
        {
            try
            {
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("cmdHDToolDuctQuantity_custom.png"))
                {
                    if (stream != null)
                    {
                        using (Bitmap origBmp = new Bitmap(stream))
                        using (Bitmap resizedBmp = new Bitmap(size, size))
                        {
                            using (Graphics g = Graphics.FromImage(resizedBmp))
                            {
                                g.SmoothingMode = SmoothingMode.AntiAlias;
                                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                                g.Clear(System.Drawing.Color.Transparent);
                                g.DrawImage(origBmp, 0, 0, size, size);
                            }
                            return ConvertToImageSource(resizedBmp);
                        }
                    }
                }
            }
            catch { }

            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // 1. Mặt trên ống gió (Top Face tôn mạ kẽm sáng)
                    PointF[] topFace = new PointF[]
                    {
                        new PointF(4f * scale, 10f * scale),
                        new PointF(22f * scale, 6f * scale),
                        new PointF(29f * scale, 12f * scale),
                        new PointF(11f * scale, 16f * scale)
                    };
                    using (SolidBrush topBrush = new SolidBrush(System.Drawing.Color.FromArgb(148, 163, 184)))
                    using (Pen borderPen = new Pen(System.Drawing.Color.FromArgb(51, 65, 85), 1.2f * scale))
                    {
                        g.FillPolygon(topBrush, topFace);
                        g.DrawPolygon(borderPen, topFace);
                    }

                    // 2. Mặt bên trái (Left Face tôn mạ kẽm tối)
                    PointF[] leftFace = new PointF[]
                    {
                        new PointF(4f * scale, 10f * scale),
                        new PointF(11f * scale, 16f * scale),
                        new PointF(11f * scale, 26f * scale),
                        new PointF(4f * scale, 20f * scale)
                    };
                    using (SolidBrush leftBrush = new SolidBrush(System.Drawing.Color.FromArgb(100, 116, 139)))
                    using (Pen borderPen = new Pen(System.Drawing.Color.FromArgb(51, 65, 85), 1.2f * scale))
                    {
                        g.FillPolygon(leftBrush, leftFace);
                        g.DrawPolygon(borderPen, leftFace);
                    }

                    // 3. Mặt bên phải (Right Face tôn mạ kẽm vừa)
                    PointF[] rightFace = new PointF[]
                    {
                        new PointF(11f * scale, 16f * scale),
                        new PointF(29f * scale, 12f * scale),
                        new PointF(29f * scale, 22f * scale),
                        new PointF(11f * scale, 26f * scale)
                    };
                    using (SolidBrush rightBrush = new SolidBrush(System.Drawing.Color.FromArgb(203, 213, 225)))
                    using (Pen borderPen = new Pen(System.Drawing.Color.FromArgb(51, 65, 85), 1.2f * scale))
                    {
                        g.FillPolygon(rightBrush, rightFace);
                        g.DrawPolygon(borderPen, rightFace);
                    }

                    // 4. Viền bích TDC nẹp C mặt trước màu xanh cyan sáng
                    using (Pen flangePen = new Pen(System.Drawing.Color.FromArgb(56, 189, 248), 2.0f * scale))
                    {
                        g.DrawLine(flangePen, 4f * scale, 10f * scale, 11f * scale, 16f * scale);
                        g.DrawLine(flangePen, 11f * scale, 16f * scale, 11f * scale, 26f * scale);
                        g.DrawLine(flangePen, 11f * scale, 26f * scale, 4f * scale, 20f * scale);
                        g.DrawLine(flangePen, 4f * scale, 20f * scale, 4f * scale, 10f * scale);
                    }

                    // 5. Gân tăng cứng chéo (Cross-breaking)
                    using (Pen crossPen = new Pen(System.Drawing.Color.FromArgb(148, 163, 184), 0.8f * scale))
                    {
                        crossPen.DashStyle = DashStyle.Dash;
                        g.DrawLine(crossPen, 11f * scale, 16f * scale, 29f * scale, 22f * scale);
                        g.DrawLine(crossPen, 11f * scale, 26f * scale, 29f * scale, 12f * scale);
                    }

                    // 6. Luồng gió khí động học màu xanh ngọc (Airflow breeze curves)
                    using (Pen windPen1 = new Pen(System.Drawing.Color.FromArgb(16, 185, 129), 1.6f * scale))
                    using (Pen windPen2 = new Pen(System.Drawing.Color.FromArgb(52, 211, 153), 1.2f * scale))
                    {
                        windPen1.StartCap = LineCap.Round; windPen1.EndCap = LineCap.Round;
                        windPen2.StartCap = LineCap.Round; windPen2.EndCap = LineCap.Round;
                        g.DrawBezier(windPen1, 1f * scale, 6f * scale, 5f * scale, 4f * scale, 8f * scale, 8f * scale, 13f * scale, 6f * scale);
                        g.DrawBezier(windPen2, 2f * scale, 8f * scale, 6f * scale, 6f * scale, 9f * scale, 10f * scale, 15f * scale, 8f * scale);
                    }

                    // 7. Huy hiệu m² diện tích tôn ở góc dưới phải (#f59e0b)
                    using (SolidBrush badgeBg = new SolidBrush(System.Drawing.Color.FromArgb(245, 158, 11)))
                    using (Pen badgeBorder = new Pen(System.Drawing.Color.White, 1.0f * scale))
                    {
                        g.FillRectangle(badgeBg, 18f * scale, 19f * scale, 13f * scale, 10f * scale);
                        g.DrawRectangle(badgeBorder, 18f * scale, 19f * scale, 13f * scale, 10f * scale);
                        if (size >= 32)
                        {
                            using (Font font = new Font("Segoe UI", 5.5f, FontStyle.Bold))
                            using (SolidBrush textBrush = new SolidBrush(System.Drawing.Color.White))
                            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                            {
                                g.DrawString("m²", font, textBrush, new RectangleF(18f * scale, 19f * scale, 13f * scale, 10f * scale), sf);
                            }
                        }
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        /// <summary>
        /// Tạo icon Ribbon cho Miệng Gió (Air Diffuser):
        /// Khung miệng gió 4 hướng thổi, nan nhôm định hình và mũi tên hướng gió xanh cyan.
        /// </summary>
        public static ImageSource CreateAirDiffuserIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // Khung viền miệng gió ngoài
                    using (SolidBrush bgBrush = new SolidBrush(System.Drawing.Color.FromArgb(241, 245, 249)))
                    using (Pen borderPen = new Pen(System.Drawing.Color.FromArgb(2, 132, 199), 1.5f * scale))
                    {
                        g.FillRectangle(bgBrush, 3f * scale, 3f * scale, 26f * scale, 26f * scale);
                        g.DrawRectangle(borderPen, 3f * scale, 3f * scale, 26f * scale, 26f * scale);
                    }

                    // Khung nan vuông cấp 2
                    using (Pen pen2 = new Pen(System.Drawing.Color.FromArgb(2, 132, 199), 1.2f * scale))
                    {
                        g.DrawRectangle(pen2, 7f * scale, 7f * scale, 18f * scale, 18f * scale);
                    }

                    // Khung nan vuông cấp 3
                    using (Pen pen3 = new Pen(System.Drawing.Color.FromArgb(2, 132, 199), 1.2f * scale))
                    {
                        g.DrawRectangle(pen3, 11f * scale, 11f * scale, 10f * scale, 10f * scale);
                    }

                    // Tâm khuếch tán gió
                    using (SolidBrush centerBrush = new SolidBrush(System.Drawing.Color.FromArgb(2, 132, 199)))
                    {
                        g.FillEllipse(centerBrush, 14f * scale, 14f * scale, 4f * scale, 4f * scale);
                    }

                    // Mũi tên gió thổi 4 hướng
                    using (Pen arrowPen = new Pen(System.Drawing.Color.FromArgb(6, 182, 212), 1.5f * scale))
                    {
                        arrowPen.StartCap = LineCap.Round; arrowPen.EndCap = LineCap.Round;
                        g.DrawLine(arrowPen, 16f * scale, 7f * scale, 16f * scale, 4f * scale);
                        g.DrawLine(arrowPen, 16f * scale, 25f * scale, 16f * scale, 28f * scale);
                        g.DrawLine(arrowPen, 7f * scale, 16f * scale, 4f * scale, 16f * scale);
                        g.DrawLine(arrowPen, 25f * scale, 16f * scale, 28f * scale, 16f * scale);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        /// <summary>
        /// Tạo icon Ribbon cho Van Gió Điều Chỉnh Lưu Lượng (Duct Damper / VCD / OBD):
        /// Thân van vuông, cánh van xoay chỉnh góc và cần gạt điều khiển màu đỏ nổi bật.
        /// </summary>
        public static ImageSource CreateDuctDamperIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // Thân vỏ van gió kim loại
                    using (SolidBrush bodyBrush = new SolidBrush(System.Drawing.Color.FromArgb(51, 65, 85)))
                    using (Pen rimPen = new Pen(System.Drawing.Color.FromArgb(148, 163, 184), 1.5f * scale))
                    {
                        g.FillRectangle(bodyBrush, 4f * scale, 6f * scale, 24f * scale, 20f * scale);
                        g.DrawRectangle(rimPen, 4f * scale, 6f * scale, 24f * scale, 20f * scale);
                    }

                    // Cánh lá van gió màu vàng cam (Damper Blades)
                    using (Pen bladePen = new Pen(System.Drawing.Color.FromArgb(245, 158, 11), 2.2f * scale))
                    {
                        bladePen.StartCap = LineCap.Round; bladePen.EndCap = LineCap.Round;
                        g.DrawLine(bladePen, 8f * scale, 11f * scale, 24f * scale, 11f * scale);
                        g.DrawLine(bladePen, 8f * scale, 16f * scale, 24f * scale, 16f * scale);
                        g.DrawLine(bladePen, 8f * scale, 21f * scale, 24f * scale, 21f * scale);
                    }

                    // Tay gạt điều chỉnh / Cụm động cơ điều khiển màu đỏ
                    using (SolidBrush knobBrush = new SolidBrush(System.Drawing.Color.FromArgb(239, 68, 68)))
                    using (Pen knobPen = new Pen(System.Drawing.Color.White, 1.0f * scale))
                    using (Pen handlePen = new Pen(System.Drawing.Color.FromArgb(239, 68, 68), 2.5f * scale))
                    {
                        handlePen.StartCap = LineCap.Round; handlePen.EndCap = LineCap.Round;
                        g.FillEllipse(knobBrush, 24f * scale, 13f * scale, 6f * scale, 6f * scale);
                        g.DrawEllipse(knobPen, 24f * scale, 13f * scale, 6f * scale, 6f * scale);
                        g.DrawLine(handlePen, 27f * scale, 16f * scale, 29f * scale, 8f * scale);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        /// <summary>
        /// Tạo icon Ribbon cho Thang Máng Cáp Điện (Cable Tray):
        /// </summary>
        public static ImageSource CreateCableTrayIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // 2 Thanh dọc thang cáp màu cam vàng
                    using (Pen railPen = new Pen(System.Drawing.Color.FromArgb(245, 158, 11), 2.5f * scale))
                    {
                        railPen.StartCap = LineCap.Round; railPen.EndCap = LineCap.Round;
                        g.DrawLine(railPen, 4f * scale, 6f * scale, 28f * scale, 6f * scale);
                        g.DrawLine(railPen, 4f * scale, 26f * scale, 28f * scale, 26f * scale);
                    }

                    // Các bước rungs ngang
                    using (Pen rungPen = new Pen(System.Drawing.Color.FromArgb(245, 158, 11), 1.5f * scale))
                    {
                        g.DrawLine(rungPen, 9f * scale, 6f * scale, 9f * scale, 26f * scale);
                        g.DrawLine(rungPen, 16f * scale, 6f * scale, 16f * scale, 26f * scale);
                        g.DrawLine(rungPen, 23f * scale, 6f * scale, 23f * scale, 26f * scale);
                    }

                    // Tia chớp điện màu vàng tia lửa ở giữa
                    PointF[] bolt = new PointF[]
                    {
                        new PointF(17f * scale, 8f * scale),
                        new PointF(11f * scale, 17f * scale),
                        new PointF(16f * scale, 17f * scale),
                        new PointF(15f * scale, 24f * scale),
                        new PointF(22f * scale, 14f * scale),
                        new PointF(17f * scale, 14f * scale)
                    };
                    using (SolidBrush boltBrush = new SolidBrush(System.Drawing.Color.FromArgb(251, 191, 36)))
                    using (Pen boltPen = new Pen(System.Drawing.Color.FromArgb(217, 119, 6), 1.0f * scale))
                    {
                        g.FillPolygon(boltBrush, bolt);
                        g.DrawPolygon(boltPen, bolt);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        /// <summary>
        /// Tạo icon Ribbon cho Kiểm Tra Va Chạm (Clash Check):
        /// </summary>
        public static ImageSource CreateClashCheckIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // Ống ngang xanh
                    using (SolidBrush b1 = new SolidBrush(System.Drawing.Color.FromArgb(2, 132, 199)))
                    using (Pen p1 = new Pen(System.Drawing.Color.FromArgb(3, 105, 161), 1f * scale))
                    {
                        g.FillRectangle(b1, 2f * scale, 13f * scale, 28f * scale, 6f * scale);
                        g.DrawRectangle(p1, 2f * scale, 13f * scale, 28f * scale, 6f * scale);
                    }

                    // Ống dọc cam
                    using (SolidBrush b2 = new SolidBrush(System.Drawing.Color.FromArgb(245, 158, 11)))
                    using (Pen p2 = new Pen(System.Drawing.Color.FromArgb(217, 119, 6), 1f * scale))
                    {
                        g.FillRectangle(b2, 13f * scale, 2f * scale, 6f * scale, 28f * scale);
                        g.DrawRectangle(p2, 13f * scale, 2f * scale, 6f * scale, 28f * scale);
                    }

                    // Vòng tròn va chạm màu đỏ dấu chấm than
                    using (SolidBrush redB = new SolidBrush(System.Drawing.Color.FromArgb(239, 68, 68)))
                    using (Pen whiteP = new Pen(System.Drawing.Color.White, 1.5f * scale))
                    {
                        g.FillEllipse(redB, 9.5f * scale, 9.5f * scale, 13f * scale, 13f * scale);
                        g.DrawEllipse(whiteP, 9.5f * scale, 9.5f * scale, 13f * scale, 13f * scale);
                        if (size >= 32)
                        {
                            using (Font f = new Font("Segoe UI", 8.0f, FontStyle.Bold))
                            using (SolidBrush wb = new SolidBrush(System.Drawing.Color.White))
                            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                            {
                                g.DrawString("!", f, wb, new RectangleF(9.5f * scale, 9.5f * scale, 13f * scale, 13f * scale), sf);
                            }
                        }
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        /// <summary>
        /// Tạo icon Ribbon cho In Bản Vẽ Hàng Loạt (Batch Print):
        /// </summary>
        public static ImageSource CreateBatchPrintIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // Thân máy in
                    using (SolidBrush b = new SolidBrush(System.Drawing.Color.FromArgb(30, 41, 59)))
                    using (Pen p = new Pen(System.Drawing.Color.FromArgb(2, 132, 199), 1.5f * scale))
                    {
                        g.FillRectangle(b, 4f * scale, 9f * scale, 24f * scale, 14f * scale);
                        g.DrawRectangle(p, 4f * scale, 9f * scale, 24f * scale, 14f * scale);
                    }

                    // Tờ giấy trên
                    using (SolidBrush paperB = new SolidBrush(System.Drawing.Color.FromArgb(248, 250, 252)))
                    using (Pen paperP = new Pen(System.Drawing.Color.FromArgb(100, 116, 139), 1f * scale))
                    {
                        g.FillRectangle(paperB, 8f * scale, 3f * scale, 16f * scale, 8f * scale);
                        g.DrawRectangle(paperP, 8f * scale, 3f * scale, 16f * scale, 8f * scale);
                    }

                    // Tờ bản vẽ in ra dưới
                    using (SolidBrush outB = new SolidBrush(System.Drawing.Color.White))
                    using (Pen outP = new Pen(System.Drawing.Color.FromArgb(148, 163, 184), 1.2f * scale))
                    {
                        g.FillRectangle(outB, 7f * scale, 17f * scale, 18f * scale, 12f * scale);
                        g.DrawRectangle(outP, 7f * scale, 17f * scale, 18f * scale, 12f * scale);
                        using (Pen lineP = new Pen(System.Drawing.Color.FromArgb(2, 132, 199), 1.5f * scale))
                        {
                            g.DrawLine(lineP, 10f * scale, 21f * scale, 22f * scale, 21f * scale);
                            g.DrawLine(lineP, 10f * scale, 25f * scale, 18f * scale, 25f * scale);
                        }
                    }

                    // Đèn tín hiệu xanh
                    using (SolidBrush ledB = new SolidBrush(System.Drawing.Color.FromArgb(16, 185, 129)))
                    {
                        g.FillEllipse(ledB, 22f * scale, 12f * scale, 3f * scale, 3f * scale);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        /// <summary>
        /// Tạo icon Ribbon cho Đồng Bộ BIM Cloud (BIM Sync):
        /// </summary>
        public static ImageSource CreateBimSyncIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // Đám mây xanh BIM
                    using (GraphicsPath cloud = new GraphicsPath())
                    {
                        cloud.AddArc(6f * scale, 14f * scale, 9f * scale, 9f * scale, 90, 180);
                        cloud.AddArc(11f * scale, 8f * scale, 11f * scale, 11f * scale, 180, 180);
                        cloud.AddArc(19f * scale, 12f * scale, 9f * scale, 9f * scale, 270, 135);
                        cloud.CloseFigure();

                        using (SolidBrush b = new SolidBrush(System.Drawing.Color.FromArgb(2, 132, 199)))
                        using (Pen p = new Pen(System.Drawing.Color.FromArgb(56, 189, 248), 1.5f * scale))
                        {
                            g.FillPath(b, cloud);
                            g.DrawPath(p, cloud);
                        }
                    }

                    // Vòng tròn xoay đồng bộ xanh ngọc
                    using (SolidBrush syncB = new SolidBrush(System.Drawing.Color.FromArgb(15, 23, 42)))
                    using (Pen syncP = new Pen(System.Drawing.Color.FromArgb(16, 185, 129), 1.8f * scale))
                    {
                        g.FillEllipse(syncB, 11f * scale, 17f * scale, 14f * scale, 14f * scale);
                        g.DrawEllipse(syncP, 11f * scale, 17f * scale, 14f * scale, 14f * scale);
                        using (Pen arrow = new Pen(System.Drawing.Color.FromArgb(52, 211, 153), 1.4f * scale))
                        {
                            g.DrawArc(arrow, 13f * scale, 19f * scale, 10f * scale, 10f * scale, 45, 180);
                            g.DrawArc(arrow, 13f * scale, 19f * scale, 10f * scale, 10f * scale, 225, 180);
                        }
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        /// <summary>
        /// Tạo icon Ribbon cho Căn Kích Thước Dim (Dimension Align):
        /// </summary>
        public static ImageSource CreateDimensionAlignIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // Thân thước đo màu vàng cam kỹ thuật
                    using (SolidBrush rB = new SolidBrush(System.Drawing.Color.FromArgb(251, 191, 36)))
                    using (Pen rP = new Pen(System.Drawing.Color.FromArgb(217, 119, 6), 1.5f * scale))
                    {
                        g.FillRectangle(rB, 3f * scale, 10f * scale, 26f * scale, 12f * scale);
                        g.DrawRectangle(rP, 3f * scale, 10f * scale, 26f * scale, 12f * scale);
                    }

                    // Vạch chia kích thước mm
                    using (Pen tickP = new Pen(System.Drawing.Color.FromArgb(120, 53, 15), 1.2f * scale))
                    {
                        g.DrawLine(tickP, 7f * scale, 10f * scale, 7f * scale, 15f * scale);
                        g.DrawLine(tickP, 11f * scale, 10f * scale, 11f * scale, 18f * scale);
                        g.DrawLine(tickP, 15f * scale, 10f * scale, 15f * scale, 15f * scale);
                        g.DrawLine(tickP, 19f * scale, 10f * scale, 19f * scale, 18f * scale);
                        g.DrawLine(tickP, 23f * scale, 10f * scale, 23f * scale, 15f * scale);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        /// <summary>
        /// Tạo Icon riêng cho tính năng "Khối Lượng Ống Nước" (Chuyên bóc mét dài ống nước)
        /// </summary>
        public static ImageSource CreatePipeOnlyIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // 1. Thân ống nước chính nằm ngang (Màu xanh dương đậm nét #0284c7)
                    using (LinearGradientBrush pipeGrad = new LinearGradientBrush(
                        new PointF(0, 10f * scale), new PointF(0, 22f * scale),
                        System.Drawing.Color.FromArgb(56, 189, 248), System.Drawing.Color.FromArgb(3, 105, 161)))
                    {
                        g.FillRectangle(pipeGrad, 2f * scale, 11f * scale, 28f * scale, 10f * scale);
                    }

                    // Viền ống
                    using (Pen pipeBorder = new Pen(System.Drawing.Color.FromArgb(2, 60, 115), 1.5f * scale))
                    {
                        g.DrawRectangle(pipeBorder, 2f * scale, 11f * scale, 28f * scale, 10f * scale);
                    }

                    // 2. Hai đầu ống (Vành gờ mặt bích / măng sông)
                    using (SolidBrush flangeB = new SolidBrush(System.Drawing.Color.FromArgb(14, 165, 233)))
                    using (Pen flangeP = new Pen(System.Drawing.Color.FromArgb(3, 105, 161), 1.2f * scale))
                    {
                        g.FillRectangle(flangeB, 1f * scale, 9f * scale, 3.5f * scale, 14f * scale);
                        g.DrawRectangle(flangeP, 1f * scale, 9f * scale, 3.5f * scale, 14f * scale);

                        g.FillRectangle(flangeB, 27.5f * scale, 9f * scale, 3.5f * scale, 14f * scale);
                        g.DrawRectangle(flangeP, 27.5f * scale, 9f * scale, 3.5f * scale, 14f * scale);
                    }

                    // 3. Vạch thước đo mét dài (m) phía dưới ống
                    using (Pen rulerP = new Pen(System.Drawing.Color.FromArgb(245, 158, 11), 1.8f * scale))
                    {
                        g.DrawLine(rulerP, 5f * scale, 26f * scale, 27f * scale, 26f * scale);
                        g.DrawLine(rulerP, 5f * scale, 23f * scale, 5f * scale, 27f * scale);
                        g.DrawLine(rulerP, 16f * scale, 24f * scale, 16f * scale, 27f * scale);
                        g.DrawLine(rulerP, 27f * scale, 23f * scale, 27f * scale, 27f * scale);
                    }

                    // 4. Giọt nước nhỏ phía trên (Màu xanh cyan tươi)
                    using (GraphicsPath dropPath = new GraphicsPath())
                    {
                        dropPath.AddBezier(
                            new PointF(16f * scale, 1f * scale),
                            new PointF(12f * scale, 6f * scale),
                            new PointF(12f * scale, 9f * scale),
                            new PointF(16f * scale, 9f * scale));
                        dropPath.AddBezier(
                            new PointF(16f * scale, 9f * scale),
                            new PointF(20f * scale, 9f * scale),
                            new PointF(20f * scale, 6f * scale),
                            new PointF(16f * scale, 1f * scale));
                        dropPath.CloseFigure();

                        using (SolidBrush dropB = new SolidBrush(System.Drawing.Color.FromArgb(56, 189, 248)))
                        {
                            g.FillPath(dropB, dropPath);
                        }
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        /// <summary>
        /// Tạo Icon riêng cho tính năng "Phụ Kiện Ống Nước" (Chuyên bóc Co, Cút, Tê, Van, Côn)
        /// </summary>
        public static ImageSource CreateFittingOnlyIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // 1. Vẽ Co Cút 90 độ (Elbow Fitting) nổi bật phong cách kỹ thuật
                    using (GraphicsPath elbowPath = new GraphicsPath())
                    {
                        elbowPath.AddLine(6f * scale, 26f * scale, 14f * scale, 26f * scale);
                        elbowPath.AddBezier(14f * scale, 26f * scale, 22f * scale, 24f * scale, 26f * scale, 18f * scale, 26f * scale, 10f * scale);
                        elbowPath.AddLine(26f * scale, 10f * scale, 26f * scale, 4f * scale);
                        elbowPath.AddLine(26f * scale, 4f * scale, 18f * scale, 4f * scale);
                        elbowPath.AddBezier(18f * scale, 4f * scale, 18f * scale, 12f * scale, 12f * scale, 18f * scale, 6f * scale, 18f * scale);
                        elbowPath.CloseFigure();

                        using (LinearGradientBrush fitGrad = new LinearGradientBrush(
                            new PointF(6f * scale, 4f * scale), new PointF(26f * scale, 26f * scale),
                            System.Drawing.Color.FromArgb(16, 185, 129), System.Drawing.Color.FromArgb(5, 150, 105)))
                        {
                            g.FillPath(fitGrad, elbowPath);
                        }

                        using (Pen fitPen = new Pen(System.Drawing.Color.FromArgb(6, 78, 59), 1.5f * scale))
                        {
                            g.DrawPath(fitPen, elbowPath);
                        }
                    }

                    // 2. Hai gờ mặt bích nối tại 2 đầu của Co Cút
                    using (SolidBrush flB = new SolidBrush(System.Drawing.Color.FromArgb(52, 211, 153)))
                    using (Pen flP = new Pen(System.Drawing.Color.FromArgb(4, 120, 87), 1.2f * scale))
                    {
                        // Mặt bích dưới
                        g.FillRectangle(flB, 3.5f * scale, 16.5f * scale, 3.5f * scale, 11f * scale);
                        g.DrawRectangle(flP, 3.5f * scale, 16.5f * scale, 3.5f * scale, 11f * scale);

                        // Mặt bích trên
                        g.FillRectangle(flB, 16.5f * scale, 2f * scale, 11f * scale, 3.5f * scale);
                        g.DrawRectangle(flP, 16.5f * scale, 2f * scale, 11f * scale, 3.5f * scale);
                    }

                    // 3. Ký hiệu huy hiệu Số Lượng (Count: #) ở góc dưới bên phải
                    using (SolidBrush badgeB = new SolidBrush(System.Drawing.Color.FromArgb(249, 115, 22)))
                    using (Pen badgeP = new Pen(System.Drawing.Color.White, 1.2f * scale))
                    {
                        g.FillEllipse(badgeB, 17f * scale, 17f * scale, 13f * scale, 13f * scale);
                        g.DrawEllipse(badgeP, 17f * scale, 17f * scale, 13f * scale, 13f * scale);
                    }

                    // Chữ ký hiệu "#" hoặc "PK" bên trong huy hiệu
                    using (Font font = new Font("Segoe UI", 7.0f * scale, FontStyle.Bold))
                    using (SolidBrush textB = new SolidBrush(System.Drawing.Color.White))
                    {
                        g.DrawString("#", font, textB, 19.5f * scale, 16.5f * scale);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

                public static ImageSource CreateAutoCropIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    g.Clear(System.Drawing.Color.Transparent);

                    float scale = size / 32.0f;

                    // 1. Khung viền Crop Box nét đứt màu đỏ / trắng ôm sát
                    using (Pen cropPen = new Pen(System.Drawing.Color.FromArgb(239, 68, 68), 1.8f * scale))
                    {
                        cropPen.DashStyle = DashStyle.Dash;
                        g.DrawRectangle(cropPen, 3f * scale, 3f * scale, 26f * scale, 26f * scale);
                    }

                    // 4 góc tay nắm Crop Box Handles
                    using (SolidBrush handleBrush = new SolidBrush(System.Drawing.Color.FromArgb(239, 68, 68)))
                    {
                        g.FillRectangle(handleBrush, 2f * scale, 2f * scale, 3f * scale, 3f * scale);
                        g.FillRectangle(handleBrush, 27f * scale, 2f * scale, 3f * scale, 3f * scale);
                        g.FillRectangle(handleBrush, 2f * scale, 27f * scale, 3f * scale, 3f * scale);
                        g.FillRectangle(handleBrush, 27f * scale, 27f * scale, 3f * scale, 3f * scale);
                    }

                    // 2. Hệ thống ống mô hình 3D bên trong (Màu tím Plumbing)
                    using (Pen pipePen = new Pen(System.Drawing.Color.FromArgb(192, 132, 252), 2.5f * scale))
                    {
                        pipePen.StartCap = LineCap.Round;
                        pipePen.EndCap = LineCap.Round;
                        // Trục đứng
                        g.DrawLine(pipePen, 16f * scale, 8f * scale, 16f * scale, 24f * scale);
                        // Nhánh rẽ ngang
                        g.DrawLine(pipePen, 16f * scale, 14f * scale, 23f * scale, 10f * scale);
                        g.DrawLine(pipePen, 23f * scale, 10f * scale, 23f * scale, 6f * scale);
                        // Nhánh rẽ trái
                        g.DrawLine(pipePen, 16f * scale, 18f * scale, 8f * scale, 14f * scale);
                    }

                    // Ký hiệu phụ kiện cút / tê tròn nhỏ
                    using (SolidBrush fitBrush = new SolidBrush(System.Drawing.Color.FromArgb(244, 114, 182)))
                    {
                        g.FillEllipse(fitBrush, 14.5f * scale, 12.5f * scale, 3f * scale, 3f * scale);
                        g.FillEllipse(fitBrush, 14.5f * scale, 16.5f * scale, 3f * scale, 3f * scale);
                        g.FillEllipse(fitBrush, 21.5f * scale, 8.5f * scale, 3f * scale, 3f * scale);
                    }

                    // 3. Huy hiệu tia sét Auto ⚡ ở góc phải
                    using (SolidBrush flashBg = new SolidBrush(System.Drawing.Color.FromArgb(245, 158, 11)))
                    using (Pen flashBorder = new Pen(System.Drawing.Color.White, 1.0f * scale))
                    {
                        g.FillEllipse(flashBg, 18f * scale, 18f * scale, 12f * scale, 12f * scale);
                        g.DrawEllipse(flashBorder, 18f * scale, 18f * scale, 12f * scale, 12f * scale);
                    }
                    // Vẽ tia sét ⚡ bằng Vector Polygon chuẩn nét
                    using (SolidBrush flashB = new SolidBrush(System.Drawing.Color.White))
                    {
                        PointF[] bolt = new PointF[] {
                            new PointF(24.5f * scale, 19f * scale),
                            new PointF(21.5f * scale, 24f * scale),
                            new PointF(23.5f * scale, 24f * scale),
                            new PointF(22f * scale, 29f * scale),
                            new PointF(26.5f * scale, 23.5f * scale),
                            new PointF(24.5f * scale, 23.5f * scale)
                        };
                        g.FillPolygon(flashB, bolt);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        /// <summary>
        /// Tạo icon vector cho công cụ Dựng Ống Gió Từ CAD Link
        /// </summary>
        public static ImageSource CreateCadToDuctIcon(int size)
        {
            using (Bitmap bmp = new Bitmap(size, size))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    g.Clear(System.Drawing.Color.Transparent);

                    float s = size / 32.0f;

                    // 1. Thân ống gió chữ nhật 3D (Xanh Cyan)
                    using (SolidBrush ductBrush = new SolidBrush(System.Drawing.Color.FromArgb(14, 165, 233)))
                    using (Pen ductPen = new Pen(System.Drawing.Color.FromArgb(3, 105, 161), 1.2f * s))
                    {
                        g.FillRectangle(ductBrush, 3f * s, 10f * s, 17f * s, 13f * s);
                        g.DrawRectangle(ductPen, 3f * s, 10f * s, 17f * s, 13f * s);
                    }

                    // Đường tim ống nét đứt màu trắng
                    using (Pen centerPen = new Pen(System.Drawing.Color.White, 1.0f * s))
                    {
                        centerPen.DashStyle = DashStyle.Dash;
                        g.DrawLine(centerPen, 3f * s, 16.5f * s, 20f * s, 16.5f * s);
                    }

                    // 2. Chân rẽ Tap (màu cam Amber) đâm ra
                    using (SolidBrush tapBrush = new SolidBrush(System.Drawing.Color.FromArgb(245, 158, 11)))
                    using (Pen tapPen = new Pen(System.Drawing.Color.FromArgb(217, 119, 6), 1.0f * s))
                    {
                        PointF[] tapPts = new PointF[] {
                            new PointF(20f * s, 13f * s),
                            new PointF(27f * s, 7f * s),
                            new PointF(30f * s, 7f * s),
                            new PointF(30f * s, 12f * s),
                            new PointF(20f * s, 19f * s)
                        };
                        g.FillPolygon(tapBrush, tapPts);
                        g.DrawPolygon(tapPen, tapPts);
                    }

                    // 3. Nhánh rẽ đâm xuống (Màu xanh ngọc Emerald)
                    using (SolidBrush brBrush = new SolidBrush(System.Drawing.Color.FromArgb(16, 185, 129)))
                    using (Pen brPen = new Pen(System.Drawing.Color.FromArgb(5, 150, 105), 1.0f * s))
                    {
                        g.FillRectangle(brBrush, 10f * s, 23f * s, 7f * s, 8f * s);
                        g.DrawRectangle(brPen, 10f * s, 23f * s, 7f * s, 8f * s);
                    }

                    // 4. Khung nhãn CAD nhỏ màu xanh Navy ở góc trái
                    using (SolidBrush cadBg = new SolidBrush(System.Drawing.Color.FromArgb(15, 23, 42)))
                    using (Pen cadBorder = new Pen(System.Drawing.Color.FromArgb(56, 189, 248), 1.0f * s))
                    using (Font font = new Font("Segoe UI", 5.5f * s, FontStyle.Bold))
                    using (SolidBrush textBrush = new SolidBrush(System.Drawing.Color.White))
                    {
                        g.FillRectangle(cadBg, 3.5f * s, 12.5f * s, 15f * s, 8f * s);
                        g.DrawRectangle(cadBorder, 3.5f * s, 12.5f * s, 15f * s, 8f * s);
                        g.DrawString("CAD", font, textBrush, 3.5f * s, 12f * s);
                    }

                    // 5. Mũi tên 3D đỏ chuyển đổi sang Revit
                    using (Pen arrowPen = new Pen(System.Drawing.Color.FromArgb(239, 68, 68), 1.5f * s))
                    {
                        g.DrawLine(arrowPen, 22f * s, 24f * s, 28f * s, 24f * s);
                        g.DrawLine(arrowPen, 28f * s, 24f * s, 25f * s, 21f * s);
                        g.DrawLine(arrowPen, 28f * s, 24f * s, 25f * s, 27f * s);
                    }
                }

                return ConvertToImageSource(bmp);
            }
        }

        private static ImageSource ConvertToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                memory.Position = 0;

                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();

                return bitmapImage;
            }
        }
    }
}
