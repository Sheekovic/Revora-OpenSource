using Revora;
using Revora.Client;
using Revora.Interfaces;
using Revora.Network.GamePackets;
using Revora.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Revora.Network.GamePackets
{
    public class RuedaDeSuerte : Writer, IPacket
    {

        public enum Tipo
        {
            Abrir = 0,
            Girar = 1,
            Loteria = 2,
            Reclamar = 3,
            VolverAGirar = 4,
            GirarDespuesDeReclamar = 5,
            Girar10Veces = 7,
        }
        private byte[] _packet;
        public RuedaDeSuerte(byte[] packet)
        {
            _packet = packet;

        }
        public RuedaDeSuerte()
        {
            _packet = new byte[17];
            WriteUInt16(9, 0, _packet);
            WriteUInt16(2811, 2, _packet);
        }
        public Tipo Tipos
        {
            get { return (Tipo)_packet[4]; }
            set { WriteByte((byte)value, 4, _packet); }
        }
        public int ID
        {
            get { return BitConverter.ToInt32(_packet, 5); }
            set { WriteInt32(value, 5, _packet); }
        }

        public byte[] ToArray()
        {
            return _packet;
        }

        public void Deserialize(byte[] buffer)
        {
            _packet = buffer;
        }

        public void Send(GameState client)
        {
            client.Send(_packet);
        }
        public static void Proceso(byte[] packet, GameState client)
        {

            var _packet = new RuedaDeSuerte(packet);
            var stage = RuedaDeSuerteTable.Etapas[(int)_packet.ID];
            switch (_packet.Tipos)
            {
                case RuedaDeSuerte.Tipo.Loteria:
                    {
                        _packet.Tipos = RuedaDeSuerte.Tipo.Girar;
                        _packet.ID = Kernel.Random.Next(-1, stage.Objetos.Length);
                        client.LastTaskRewardID = _packet.ID;
                        client.Send(_packet);
                        break;
                    }
                case RuedaDeSuerte.Tipo.Reclamar:
                    {
                        if (client.LastTaskRewardID > 0)
                        {
                            if (client.Entity.LuckyWheel < 10)
                            {
                                var itemID = (uint)stage.Objetos[client.LastTaskRewardID];
                                client.Inventory.AddTime(itemID, 3);
                                client.LastTaskRewardID = 0;
                                client.Entity.LuckyWheel++;
                            }
                        }
                        break;
                    }
                case RuedaDeSuerte.Tipo.VolverAGirar:
                    {
                        #region Opcion Inabilitada(Se Habilita Opcionalmente)
                        //if (client.Player.ConquerPoints < stage.Costos)
                        //{
                        //    client.Send(new MsgTalk("No Tienes CPs (Tu Necesitas " +
                        //        stage.Costos + ")", System.Drawing.Color.Red, MsgTalk.System));
                        //    return;
                        //}
                        //client.Player.ConquerPoints -= stage.Costos;
                        //_packet.Tipos = RuedaDeSuerte.Tipo.Girar;
                        //_packet.ID = Kernel.Random.Next(-1, stage.Objetos.Length);
                        //client.LastTaskRewardID = _packet.ID;
                        //client.Send(_packet);
                        #endregion
                        break;
                    }
                case RuedaDeSuerte.Tipo.GirarDespuesDeReclamar:
                    {
                        #region Opcion Inabilitada(Se Habilita Opcionalmente)
                        //if (client.Player.ConquerPoints < stage.Costos)
                        //{
                        //    client.Send(new MsgTalk("Usted No Tiene Cps (Tu Necesitas " +
                        //        stage.Costos + ")", System.Drawing.Color.Red, MsgTalk.System));
                        //    return;
                        //}
                        //client.Player.ConquerPoints -= stage.Costos;
                        //_packet.Tipos = RuedaDeSuerte.Tipo.Girar;
                        //_packet.ID = Kernel.Random.Next(-1, stage.Objetos.Length);
                        //client.LastTaskRewardID = _packet.ID;
                        //client.Send(_packet);
                        //MsgHandler.Process(new RuedaDeSuerte()
                        //{
                        //    Tipos = RuedaDeSuerte.Tipo.Reclamar,
                        //    ID = stage.ID
                        //}.ToArray(), client);
                        #endregion
                        break;
                    }
                case RuedaDeSuerte.Tipo.Girar10Veces:
                    {
                        //_packet.Tipos = RuedaDeSuerte.Tipo.Girar;
                        //_packet.ID = Kernel.Random.Next(-1, stage.Objetos.Length);
                        //client.LastTaskRewardID = _packet.ID;
                        //client.Send(_packet);
                        client.MessageBox("Esta Funcion No Esta Disponible Por El Momento!");
                        break;
                    }
                default: Console.WriteLine("[Rueda] Accion Desconocida " + _packet.Tipos); break;
            }
        }
    }
}