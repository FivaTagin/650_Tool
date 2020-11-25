using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace CyControl
{
    public partial class Form2 : Form
    {
        // vendor variables
        public short[] dataFrame = new short[88064];
        public int currentPrecision = (int)Precision.x1;

        public void updateForm2()
        {
            // clear and update series
            foreach (var s in this.chart1.Series)
            {
                s.Points.Clear();
                for (int index = 0; index < currentPrecision; index++)
                {
                    s.Points.AddXY(index, dataFrame[index]);
                }
            }
              



        }


        public Form2()
        {
            InitializeComponent();
            initCharts();
        }

        // vendor functions 
        private void initCharts()
        {

            int[,] array = new int[,] {
            {1,8,9,7,105,11,50,999,500,1},
            {12,15,11,18,733,5,4,3,2,500} };


            //標題 最大數值
            Series series1 = new Series("第一條線", 1000);
            

            //設定線條顏色
            series1.Color = Color.Blue;
            

            //設定字型
            series1.Font = new System.Drawing.Font("新細明體", 14);
            

            //折線圖
            series1.ChartType = SeriesChartType.Line;
           

            //將數值顯示在線上
            series1.IsValueShownAsLabel = true;
            


            //將數值新增至序列
            for (int index = 0; index < 10; index++)
            {
                series1.Points.AddXY(index, array[0, index]);
                
            }

            //將序列新增到圖上
            this.chart1.Series.Add(series1);
            

            //標題
            this.chart1.Titles.Add("標題");
        }

        private void Form2_Load(object sender, EventArgs e)
        {

        }
    }
    public enum Precision
    {
        x1 = 344,
        x2 = 141,
        x4 = 83,
        x8 = 40,
        x16= 34,
        x32= 32
    }

    public struct Coords
    {
        public Coords(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }

        public override string ToString() => $"({X}, {Y})";
    }

}
