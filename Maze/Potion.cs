using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Diagnostics;

using System.Runtime.Serialization;
using System.Xml;

namespace Maze
{
//    delegate void Use(Entity e); // XXX Scroll で定義済み

    [Serializable]
    class Potion : Entity
    {
        public override string name             // name property ... potiondef で identified かどうか見て、nickname or realnameを返す 
        {
            get
            {
                bool identified = false;
                for (int i = 0; i < potiondef.Count(); i++)
                {
                    if (potiondef[i].realname == this.realname)
                    {
                        identified = potiondef[i].identified;
                        break;
                    }
                }
                return ((identified) ? realname : nickname);
            }
            // set は、なし
        }

        private Use myuse;                      // インスタンスごとの use() の実体をしまうところ
        private string nickname;                // 識別されていない時の名前
        private string realname;                // 識別済みの時の名前
        private Random rnd = new Random();

        //
        // ポーションの諸元
        //
        private struct PotionDefine
        {
            public string nickname;
            public string realname;
            public Use myuse;
            public bool identified;

            public PotionDefine (string nickname, Use myuse, string realname) {
                this.nickname = nickname;
                this.myuse     = myuse;
                this.realname  = realname;
                this.identified = false;
            }
        };
        static PotionDefine[] potiondef = new PotionDefine[] {
                                       new PotionDefine("Green Potion", useHealing,      "Healing Potion"),
                                       new PotionDefine("Red Potion",   usePoison,       "Poison Potion"),
                                       new PotionDefine("Gold Potion",  useGainStrength, "Gain Strength Potion"),
                                       new PotionDefine("Black Potion", useLoseStrength, "Lose Strength Potion"),
                                       new PotionDefine("Purple Potion", useAmnesia,     "Amnesia Potion"),
                                   };

        // スタティックコンストラクタで、nickname をシャッフルしておく
        static Potion()
        {
            Random rnd = new Random();
            for (int i = 0; i < potiondef.Count() * 3; i++)
            {
                int j = rnd.Next(potiondef.Count());
                int k = rnd.Next(potiondef.Count());
                if (i != k)
                {
                    string tmp = potiondef[j].nickname;
                    potiondef[j].nickname = potiondef[k].nickname;
                    potiondef[k].nickname = tmp;
                }
            } 
        }

        public Potion(MazeAlgo maze, int floor)
            : base(maze)
        {
            graph = graphOrig = '!';

            int i;                      // potiondef の番号
            int r = rnd.Next(100);
            if (r < 40) i = 0;
            else if (r < 50) i = 1;
            else if (r < 70) i = 2;
            else if (r < 80) i = 3;
            else i = 4;

            nickname = potiondef[i].nickname;
            myuse    = potiondef[i].myuse;
            realname = potiondef[i].realname;
        }

        //
        // ここから薬の実装
        //
        static private void useHealing(Entity user)
        {
            Console.WriteLine("おいしい");
            user.hit = user.hitmax;
        }

        static private void usePoison(Entity user)
        {
            Console.WriteLine("おなかがいたい");
            user.hit--;
        }

        static private void useGainStrength(Entity user)
        {
            Console.WriteLine("力がみなぎる");
            if (user.strength < user.strengthmax)
            {
                user.strength = user.strengthmax;
            }
            else
            {
                user.strength = ++user.strengthmax;
            }
        }

        static private void useLoseStrength(Entity user)
        {
            Console.WriteLine("脱力している");
            if (user.strength > 0) user.strength--;
        }

        static private void useAmnesia(Entity user)
        {
            Console.WriteLine("あれ？");
            user.amnesia = true;
        }

        //
        // Entity としての性質
        //
        public override bool isUsable()
        {
            return true;
        }

        public override void use(Entity e)
        {
            myuse(e);
        }

        public override void pickup(Entity user)
        {
            bool found = false;
            foreach (Item i in user.itemlist)
            {
                if (i.name == this.name)
                {
                    i.add();
                    found = true;
                    break;
                }
            }
            if (!found) user.itemlist.Add(new Item(this));

            Console.WriteLine("{0} は {1} を拾った", user.name, this.name);
        }

        public override bool isIdentified()
        {
            for (int i = 0; i < potiondef.Count(); i++)
            {
                if (potiondef[i].realname == this.realname)
                {
                    return potiondef[i].identified;
                }
            }
            Debug.Assert(false, "should return before here");
            return false;
        }

        public override void identify(Entity user)              // potiondef[].identified = true にすることで、name プロパティが realname を返すようになる
        {
            for (int i = 0; i < potiondef.Count(); i++)
            {
                if (potiondef[i].realname == this.realname)
                {
                    potiondef[i].identified = true;
                    return;
                }
            }
            Debug.Assert(false, "should return before here"); ;
        }

    }
}
