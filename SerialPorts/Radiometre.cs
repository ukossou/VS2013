﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.IO.Ports;
using System.Timers;
using System.Text;

namespace SerialPorts
{
    class Radiometre
    {
        private SerialPort PortSerie;
        private int FrequenceRad = 0;
        private System.Timers.Timer TimerReception;//Timer pour detecter si le radiometre cesse de transmettre 
        private int DELAIS_REC_MAX = 60000;
        private DirectoryInfo RepCourant;
        private DateTime DateCourante;
        private System.Timers.Timer TimerJour;
        private StreamWriter FichierCourant;
        private Queue DonneeLues;
        private string NomPort;
        private Thread ThreadEcriture;
        private bool RSenvoye = false;
        public bool started = false;

        public Radiometre(string nom)
        {
            NomPort = nom;
        }

        public void demarrer()
        {


            lock (this)
            {

                bool res = false;
                try
                {
                    res = initialiserPort(NomPort, 38400, 10000);
                }
                finally
                {
                    if(!res)
                        Console.WriteLine("...ECHEC...ouverture... " + PortSerie.PortName + "\n");  
                }
                if (res)//reussite de l'initialisation
                {
                    int nbTestCommunication = 1;
                    ThreadEcriture = new Thread(ecrireDisque);

                    //Initialisations 
                    initialiserTimerJour();
                    //tester la communication 
                    if (testerCommunication(nbTestCommunication))
                    {
                        FrequenceRad = detecterFrequence();
                        Console.WriteLine("\n...Reception OK sur ... " + PortSerie.PortName
                                            + "===" + FrequenceRad + "===\n");
                        //creer le repertoire qui contiendra les mesures
                        RepCourant = Directory.CreateDirectory("Rad-" + FrequenceRad);

                        //creer le fichier de donnees 
                        creerFichier();

                        //Initialisation du tampon de donnees
                        DonneeLues = Queue.Synchronized(new Queue());

                        //Initialisation de TimerReception
                        TimerReception = new System.Timers.Timer(DELAIS_REC_MAX);
                        TimerReception.Elapsed += new ElapsedEventHandler(depassementTimerReception);

                        //ajout de l'evenement DataReceived
                        PortSerie.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);

                        //Demarrage de TimerReception et de l'ecriture
                        TimerReception.Start();
                        ThreadEcriture.Start();
                        started = true;

                    }
                }
                if (!started)
                    Console.WriteLine("Echec sur le " + NomPort);
                /*
                if(started)
                {
                    long lastSize = 0;
                    long actualSize = 100;
                    while (actualSize>=lastSize)
                    {
                        lastSize = FichierCourant.BaseStream.Length;
                        Thread.Sleep(3600000);
                        actualSize = FichierCourant.BaseStream.Length;
                    }
                    Console.WriteLine("Plus d'ecriture a " + DateTime.Now.ToString());    
                }*/
                    
            }
        }

        public void attendre()
        {
            if (started)
            {
                long lastSize = 0;
                long actualSize = 100;
                while (actualSize >= lastSize)
                {
                    lastSize = FichierCourant.BaseStream.Length;
                    Thread.Sleep(3600000);
                    actualSize = FichierCourant.BaseStream.Length;
                }
                Console.WriteLine("Plus d'ecriture a " + DateTime.Now.ToString());
            }

        }

        public void finaliser()
        {
            if (started)
            {
                ThreadEcriture.Abort();
                ThreadEcriture.Join();
            }

            if (FichierCourant != null)
                FichierCourant.Dispose();
            //fermeture du port serie
            if (PortSerie != null)
            {
                PortSerie.Dispose();
                Console.WriteLine("***Fermeture radiometre**** " + FrequenceRad);
            }

        }


        private void initialiserTimerJour()
        {
            DateTime demain = DateTime.Now.Date.AddDays(1);
            TimeSpan ecartDemain = demain.Subtract(DateTime.Now);
            TimerJour = new System.Timers.Timer(ecartDemain.TotalMilliseconds);

            TimerJour.Elapsed += new ElapsedEventHandler(depassementTimerJour);
            DateCourante = DateTime.Now;
            TimerJour.Start();

        }
        private void creerFichier()
        {
            //creer le fichier de donnees
            char sep = Path.DirectorySeparatorChar;
            string cheminFich = RepCourant.Name + sep
                                + "MesDu-" + DateCourante.ToString("d")
                                + ".txt";
            FileStream fileStream = new FileStream(cheminFich, FileMode.Append, FileAccess.Write, FileShare.Read);
            FichierCourant = new StreamWriter(fileStream);

            //Verifier si le fichier existe deja avant d'ecrire 
            if (FichierCourant.BaseStream.Length <= 100)
                FichierCourant.WriteLine(creerEntete());

        }
        private string creerEntete()
        {
            StringBuilder entete = new StringBuilder();
            entete.Append(String.Format("{0,-20}", "Date"));
            entete.Append(String.Format("{0,-10}", ",Rad Time"));
            entete.Append(String.Format("{0,-5}", ",Rec"));
            entete.Append(String.Format("{0,-9}", ",PWM"));
            entete.Append(String.Format("{0,-9}", ",Tec V"));
            entete.Append(String.Format("{0,-9}", ",T_ant"));
            entete.Append(String.Format("{0,-9}", ",T_load"));
            entete.Append(String.Format("{0,-9}", ",T_IF"));
            entete.Append(String.Format("{0,-9}", ",T_case"));
            entete.Append(String.Format("{0,-7 }", ",Supply"));
            entete.Append(String.Format("{0,-9}", ",V-NdOn"));
            entete.Append(String.Format("{0,-8}", ",V-NdOff"));
            entete.Append(String.Format("{0,-6}", ",Flag"));
            entete.Append(String.Format("{0,-10}", ",Angle"));
            entete.Append(String.Format("{0,-9}", ",Temp"));
            entete.Append(String.Format("{0,-11}", ",X-Data"));
            entete.Append(String.Format("{0,-11}", ",Y-Data"));

            entete.Append(String.Format("{0,-20}", ",Date"));
            entete.Append(String.Format("{0,-10}", ",Rad Time"));
            entete.Append(String.Format("{0,-5}", ",Rec"));
            entete.Append(String.Format("{0,-8}", ",Freq"));
            entete.Append(String.Format("{0,-10}", ",Vsky-V"));
            entete.Append(String.Format("{0,-10}", ",Vsky-V+ND"));
            entete.Append(String.Format("{0,-10}", ",Vsky-H"));
            entete.Append(String.Format("{0,-10}", ",Vsky-V+ND"));
            entete.Append(String.Format("{0,-10}", ",V_load"));
            entete.Append(String.Format("{0,-10}", ",V_load+ND"));
            entete.Append(String.Format("{0,-10}", ",Tsky-V"));
            entete.Append(String.Format("{0,-10}", ",Tsky-H"));
            entete.Append(String.Format("{0,-10}", ",Tsky(V-H)"));

            return entete.ToString();
        }

        private void ecrireDisque()
        {
            string[] separateur = new string[] { "," };
            string[] mots;
            string ligne;
            int borne = 100;

            int compteurAffichage = 0;

            int typeData = 0;//type de donnee envoyee par le radiometre "21" ou "11"
            while (true)
            {
                if (DonneeLues.Count > borne)
                {
                    if (compteurAffichage % 1000 == 0)
                        Console.WriteLine("Ecriture sur disque " + PortSerie.PortName + " " + DateTime.Now.ToString());
                    compteurAffichage += 1;

                    lock (FichierCourant)
                    {
                        for (int i = 0; i < borne; i++)
                        {
                            ligne = (string)DonneeLues.Dequeue();
                            mots = ligne.Split(separateur, StringSplitOptions.RemoveEmptyEntries);

                            //verification de la validite du format de la donnee
                            if (mots.Length >= 13 && mots.Length <= 17)
                            {
                                string aEcrire = string.Join(" , ", mots);
                                typeData = Convert.ToInt32(mots[2]);
                                //Commencer les lignes avec les "11"
                                if (typeData == 11)
                                {
                                    aEcrire = aEcrire.Remove(aEcrire.Length - 1);
                                    aEcrire += " ";
                                }
                                FichierCourant.Write(aEcrire);
                                //Console.WriteLine("Type " + typeData + " " + aEcrire);
                            }
                            else
                            {
                                Console.WriteLine("Donne invalide " + ligne);
                            }
                            //Console.WriteLine("ligne "+ligne);
                            //FichierCourant.WriteLine(ligne); 
                        }
                    }

                }
            }
        }
        private bool testerCommunication(int maxEssais = 10)
        {
            bool bonneComm = false;
            int nombreEssais = 0;
            //PortSerie.DiscardInBuffer();
            while (bonneComm == false && nombreEssais < maxEssais)
            {
                try
                {
                    PortSerie.ReadByte();
                    bonneComm = true;
                }
                catch (TimeoutException)
                {
                    Console.WriteLine("Pas de donnes sur le port " + PortSerie.PortName);
                }
                finally
                {
                    //Console.WriteLine("Dans le finally du Timeout de testerCommunication() ");
                    //Envoyer les RS et attendre un peu avec un timer
                    if (!bonneComm)
                    {
                        Console.WriteLine("Attente de 10 secondes");
                        Console.WriteLine("Envoi de RS essai " + (nombreEssais + 1) + " sur " + maxEssais);
                        //PortSerie.Write("RS" + Convert.ToChar(13));
                        //RSenvoye = true;
                        Thread.Sleep(10000);
                    }

                }
                nombreEssais += 1;
            }
            return bonneComm;
        }
        private int detecterFrequence()
        {
            int valeurFreq = 0;
            string[] separateur = new string[] { "," };
            string[] mots;
            string ligne="";

            PortSerie.DiscardInBuffer();
            int typeData = 0;//type de donnee envoyee par le radiometre "21" ou "11"

            while (typeData != 21)
            {
                //portSerie.ReadLine();
                try
                {
                    ligne = PortSerie.ReadLine();
                }
                catch (System.TimeoutException)
                {}
                finally { };
                //Console.WriteLine("Frequence lue : " + ligne);
                mots = ligne.Split(separateur, StringSplitOptions.RemoveEmptyEntries);
                //Console.WriteLine("\n Mots lus : " + String.Join(" | ", mots));
                if (mots.Length >= 13)
                {
                    try{typeData = Convert.ToInt32(mots[2]);}
                    catch (FormatException) { }
                    finally{ }
                }
                if (typeData == 21)
                    valeurFreq = Convert.ToInt32(mots[3]);
            }

            return valeurFreq;
        }

        private bool initialiserPort(string nom, int baudRate, int timeout)
        {
            bool succes = false;
            PortSerie = new SerialPort();
            PortSerie.PortName = nom;
            PortSerie.BaudRate = baudRate;
            PortSerie.ReadTimeout = 10000;

            //Ouverture du port serie
            PortSerie.Open();
            if (PortSerie.IsOpen)
                succes = true;

            return succes;
        }

        private void depassementTimerJour(Object source, ElapsedEventArgs e)
        {
            TimerJour.Stop();
            Console.WriteLine("Changement de Jour   {0}", e.SignalTime);
            Console.WriteLine("Creation du nouveau fichier pour " + FrequenceRad);

            initialiserTimerJour();

            FichierCourant.Close();
            creerFichier();
        }

        private void depassementTimerReception(Object source, ElapsedEventArgs e)
        {
            Console.WriteLine("Plus de Reception depuis  {0}", e.SignalTime);
            Console.WriteLine("Envoi de RS ");
            PortSerie.Write("RS" + Convert.ToChar(13));
            RSenvoye = true;
        }
        private void DataReceivedHandler(
                        object sender,
                        SerialDataReceivedEventArgs e)
        {
            TimerReception.Stop();
            SerialPort sp = (SerialPort)sender;
            if (RSenvoye)
            {
                sp.DiscardInBuffer();
                RSenvoye = false;
            }

            while (sp.BytesToRead > 0)
            {
                string indata = sp.ReadLine();
                //enlever le premier champ du radiometre
                //pour inserer l'lheure courante
                indata = indata.Remove(0, 7);
                indata = DateTime.Now.ToString() + indata;
                DonneeLues.Enqueue(indata);
            }

            TimerReception.Start();

            //Console.WriteLine("Taille de DonneesLues " + Convert.ToString(DonneeLues.Count));

        }
    }
}