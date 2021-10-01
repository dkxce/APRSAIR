using System;
using System.Collections.Generic;
using System.Text;

namespace DTMFCoder
{
    public class ToneDetectorCore
    {
        /*
        * ������ �������, � ������� �������� ������������ ������.
        * ������������ �������� � ������������������ ������ ����
        * ��� ������� � 2 ���� ������.
        * ���������� ��������: 10 ... 100 ��
        */
        private static byte BUFFER_MS = 20;

        /*
        * �������� �������. �������� � ���������� � ��� �� �����,
        * ���� ����������� ������� ���������� �������.
        */
        private float m_inPower = 0.0f;

        private float m_threshold = 0.5f;
        private bool m_bDualTone = false;
        private int m_counter = 0;
        private int m_decodeLength = 0;

        private float[][] m_pSin = null;
        private float[][] m_pCos = null;
        private float[] m_pSinAccum = null;
        private float[] m_pCosAccum = null;

        public ToneDetectorCore() { }

        public bool initDTMF(int sampleRate)
        {
            int[] pFrequency = new int[8];
            pFrequency[0] = 697;
            pFrequency[1] = 770;
            pFrequency[2] = 852;
            pFrequency[3] = 941;
            pFrequency[4] = 1209;
            pFrequency[5] = 1336;
            pFrequency[6] = 1477;
            pFrequency[7] = 1633;
            bool res = initSTMF(sampleRate, pFrequency);
            m_bDualTone = true;
            return res;
        }

        public bool initSTMF(int sampleRate, int[] pFrequency)
        {
            m_pSin = new float[pFrequency.Length][];
            m_pCos = new float[pFrequency.Length][];
            m_pSinAccum = new float[pFrequency.Length];
            m_pCosAccum = new float[pFrequency.Length];
            m_inPower = 0.0f;
            m_decodeLength = (int)(BUFFER_MS * sampleRate / 1000);
            m_counter = 0;
            for (int f = pFrequency.Length; f-- != 0; )
            {
                m_pSin[f] = new float[m_decodeLength];
                m_pCos[f] = new float[m_decodeLength];
                double step = 2.0 * Math.PI * pFrequency[f] / sampleRate;
                double t = 0;
                for (int i = 0; i < m_decodeLength; i++)
                {
                    double angle = i * step;
                    m_pSin[f][i] = (float)Math.Sin(angle);
                    m_pCos[f][i] = (float)Math.Cos(angle);
                    t += m_pSin[f][i] * m_pSin[f][i];
                };
                m_pSinAccum[f] = 0.0f;
                m_pCosAccum[f] = 0.0f;
            };
            return true;
        }

        /************************************************
        ���������� �����
        0.00 < threshold < 1.00
        ************************************************/

        public void setThreshold(float threshold)
        {
            // ����������� ������� ���������, �������
            // ���������� ������� ������
            m_threshold = threshold * threshold;
        }

        //frhigh   1209  1336  1477  1633
        //frlow
        //    697   1     2     3     A
        //    770   4     5     6     B
        //    852   7     8     9     C
        //    941   *     0     #     D

        public static char[] decodeDTMF = new char[]{
                 '1', '2', '3', 'A',
                 '4', '5', '6', 'B',
                 '7', '8', '9', 'C',
                 '*', '0', '#', 'D' };

        public bool process(short sample, out char code)
        {
            code = '\0';
            for (int f = m_pSinAccum.Length; f-- != 0; )
            {
                m_pSinAccum[f] += (float)sample * m_pSin[f][m_counter];
                m_pCosAccum[f] += (float)sample * m_pCos[f][m_counter];
                m_inPower += (float)sample * (float)sample;// ���� ������������ ��������
            };
            if (++m_counter < m_decodeLength) return false;
            m_counter = 0;            
            if (m_inPower > 0.0f)
            {
                int firstFr = 0;
                int secondFr = 0;
                float max = -1.0f;
                int f = 0;
                for (; f < (m_pSinAccum.Length / 2); f++)
                {
                    float res = m_pSinAccum[f] * m_pSinAccum[f] + m_pCosAccum[f] * m_pCosAccum[f];
                    if (max < res)
                    {
                        max = res;
                        firstFr = f;
                    };
                    m_pSinAccum[f] = 0.0f;
                    m_pCosAccum[f] = 0.0f;
                };
                // ���������� ��� ��������� � �������
                /// ���� ����������� ������ �������
                // 32767 - ������������ �������� ��� 16-�� ���������� �����
                // max = max / m_decodeLength / m_decodeLength * 4.0f / (32767 * 32767);
                /// ���� ������������ ��������
                // �������� ������� = (sqrt(2)/2)^2 * m_decodeLength
                max = max / m_inPower / m_decodeLength * 16.0f;
                if (max < m_threshold) firstFr = m_pSinAccum.Length;
                if (m_bDualTone) max = -1.0f;

                for (; f < m_pSinAccum.Length; f++)
                {
                    float res = m_pSinAccum[f] * m_pSinAccum[f] + m_pCosAccum[f] * m_pCosAccum[f];
                    if (max < res)
                    {
                        max = res;
                        secondFr = f;
                    };
                    m_pSinAccum[f] = 0.0f;
                    m_pCosAccum[f] = 0.0f;
                };

                // ���������� ��� ��������� � �������
                /// ���� ����������� ������ �������
                // 32767 - ������������ �������� ��� 16-�� ���������� �����
                //max = max / m_decodeLength / m_decodeLength * 4.0f / (32767 * 32767);
                /// ���� ������������ ��������
                // �������� ������� = (sqrt(2)/2)^2 * m_decodeLength

                max = max / m_inPower / m_decodeLength * 16.0f;
                if (max < m_threshold) secondFr = m_pSinAccum.Length;
                if (m_bDualTone)
                {
                    /**
                     * ��� ���������� ����������� ������ ������������ ����� ����� ����� ��������
                     * �������������� ��������:
                     * - ������ ������������ �������� �� ������ ������ ����������
                     * - ��������� ������� ������ ���� �� ������ ������ ������ ��������
                     */
                    if ((firstFr < m_pSinAccum.Length) && (secondFr < m_pSinAccum.Length))
                        code = decodeDTMF[secondFr - (m_pSinAccum.Length / 2) +
                                          firstFr * (m_pSinAccum.Length / 2)];

                }
                else
                {
                    /**
                     * ��� ���������� ����������� ������ ������������ ����� ����� ����� ��������
                     * �������������� ��������:
                     * - ������� ������������� ������� ������ ������ ������������ ���������
                     */
                    if (secondFr < m_pSinAccum.Length)
                        code = (char)('1' + secondFr);
                    else if (firstFr < m_pSinAccum.Length)
                        code = (char)('1' + firstFr);
                };
                m_inPower = 0.0f;
            };

            return true;
        }
    }

    public class ToneDetectorHz
    {
        /**
        * ������ �������, � ������� �������� ������������ ������.
        * ������������ �������� � ������������������ ������ ����
        * ��� ������� � 2 ���� ������.
        * ���������� ��������: 10 ... 100 ��
        */

        private static byte BUFFER_MS = 20;
        /**
        * �������� �������. �������� � ���������� � ��� �� �����,
        * ���� ����������� ������� ���������� �������.
        */

        private float m_inPower = 0.0f;
        private float m_threshold = 0.5f;// ����� ���������
        private bool m_bSingleTone = false;// �������������/������������ ��������
        private int m_counter = 0;// ������� ������������ �������
        private int m_decodeLength = 0;// ����� ������� �� �������� ����� �������
        private float[] m_pV1 = null;// ������ ��� ��������
        private float[] m_pV2 = null;// ������ ��� ��������
        private float[] m_pCoeff = null;// ������������ �������

        public ToneDetectorHz() { }

        public bool initDTMF(int sampleRate)
        {
            int[] pFrequency = new int[8];
            pFrequency[0] = 697;
            pFrequency[1] = 770;
            pFrequency[2] = 852;
            pFrequency[3] = 941;
            pFrequency[4] = 1209;
            pFrequency[5] = 1336;
            pFrequency[6] = 1477;
            pFrequency[7] = 1633;
            bool res = initSTMF(sampleRate, pFrequency);
            m_bSingleTone = false;
            return res;
        }

        public bool initSTMF(int sampleRate, int[] pFrequency)
        {
            m_bSingleTone = true;
            m_pV1 = new float[pFrequency.Length];
            m_pV2 = new float[pFrequency.Length];
            m_pCoeff = new float[pFrequency.Length];
            m_inPower = 0.0f;
            m_decodeLength = (int)(BUFFER_MS * sampleRate / 1000);
            m_counter = 0;
            for (int f = pFrequency.Length; f-- != 0; )
            {
                m_pCoeff[f] = (float)(2.0 * Math.Cos(2.0 * Math.PI * pFrequency[f] / sampleRate));
                m_pV1[f] = 0.0f;
                m_pV2[f] = 0.0f;
            };
            return true;
        }

        /************************************************
        ���������� �����
        0.00 < threshold < 1.00
        ************************************************/

        public void setThreshold(float threshold)
        {
            // ����������� ������� ���������, �������
            // ���������� ������� ������
            m_threshold = threshold * threshold;
        }

        //frhigh   1209  1336  1477  1633
        //frlow
        //    697   1     2     3     A
        //    770   4     5     6     B
        //    852   7     8     9     C
        //    941   *     0     #     D

        private static char[] decodeDTMF = new char[]{
                 '1', '2', '3', 'A',
                 '4', '5', '6', 'B',
                 '7', '8', '9', 'C',
                 '*', '0', '#', 'D' };

        public bool process(short sample, out char code)
        {
            code = '\0';

            for (int f = m_pCoeff.Length; f-- != 0; )
            {
                float t = m_pV1[f];
                m_pV1[f] = (float)sample + m_pCoeff[f] * m_pV1[f] - m_pV2[f];
                m_pV2[f] = t;
                m_inPower += (float)sample * (float)sample;// ���� ������������
            }

            if (++m_counter < m_decodeLength) return false;
            m_counter = 0;            
            if (m_inPower > 0.0f)
            {
                int firstFr = 0;
                int secondFr = 0;
                float max = -1.0f;
                int f = 0;
                for (; f < (m_pCoeff.Length / 2); f++)
                {
                    float res = m_pV1[f] * m_pV1[f] + m_pV2[f] * m_pV2[f] - m_pCoeff[f] * m_pV1[f] * m_pV2[f];
                    if (max < res)
                    {
                        max = res;
                        firstFr = f;
                    };
                    m_pV1[f] = 0.0f;
                    m_pV2[f] = 0.0f;
                };

                // ���������� ��� ��������� � �������
                /// ���� ����������� ������ �������
                // 32767 - ������������ �������� ��� 16-�� ���������� �����
                // max = max / m_decodeLength / m_decodeLength * 4.0f / (32767 * 32767);
                /// ���� ������������ ��������
                // �������� ������� = (sqrt(2)/2)^2 * m_decodeLength

                max = max / m_inPower / m_decodeLength * 16.0f;
                if (max < m_threshold) firstFr = m_pCoeff.Length;
                if (!m_bSingleTone) max = -1.0f;

                for (; f < m_pCoeff.Length; f++)
                {
                    float res = m_pV1[f] * m_pV1[f] + m_pV2[f] * m_pV2[f] - m_pCoeff[f] * m_pV1[f] * m_pV2[f];
                    if (max < res)
                    {
                        max = res;
                        secondFr = f;
                    };
                    m_pV1[f] = 0.0f;
                    m_pV2[f] = 0.0f;
                };

                // ���������� ��� ��������� � �������
                // ���� ����������� ������ �������
                // 32767 - ������������ �������� ��� 16-�� ���������� �����
                // max = max / m_decodeLength / m_decodeLength * 4.0f / (32767 * 32767);
                // ���� ������������ ��������
                // �������� ������� = (sqrt(2)/2)^2 * m_decodeLength

                max = max / m_inPower / m_decodeLength * 16.0f;
                if (max < m_threshold) secondFr = m_pCoeff.Length;
                if (m_bSingleTone)
                {
                    /**
                     * ��� ���������� ����������� ������ ������������ ����� ����� ����� ��������
                     * �������������� ��������:
                     * - ������� ������������� ������� ������ ������ ������������ ���������
                     */
                    if (secondFr < m_pCoeff.Length)
                        code = (char)('1' + secondFr);
                    else if (firstFr < m_pCoeff.Length)
                        code = (char)('1' + firstFr);
                }
                else
                {
                    /**
                     * ��� ���������� ����������� ������ ������������ ����� ����� ����� ��������
                     * �������������� ��������:
                     * - ������ ������������ �������� �� ������ ������ ����������
                     * - ��������� ������� ������ ���� �� ������ ������ ������ ��������
                     */
                    if ((firstFr < m_pCoeff.Length) && (secondFr < m_pCoeff.Length))
                        code = decodeDTMF[secondFr - (m_pCoeff.Length / 2) + firstFr * (m_pCoeff.Length / 2)];
                };
                m_inPower = 0.0f;
            };

            return true;
        }

        public string DecodeDTMF(short[] samples)
        {
            return DecodeDTMF(samples, 100);
        }

        public string DecodeDTMF(short[] samples, int maxSilense_ms)
        {
            string res = "";

            char prev = '\0';
            int emptyCounter = 0;
            int maxSpace = maxSilense_ms / 20; // 1 frame
            for (int i = 0; i < samples.Length; i++)
            {
                char tone;
                if (this.process(samples[i], out tone)) // 20ms
                {
                    if (tone != '\0')
                    {
                        emptyCounter = 0;
                        if (tone != prev)
                            res += tone;
                        prev = tone;
                    }
                    else emptyCounter++;
                };
                if (emptyCounter == maxSpace) prev = '\0';
            };

            return res;
        }
    }
}
