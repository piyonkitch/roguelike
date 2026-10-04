/*
Copyright(c) 2015, piyonkitch<kazuo.horikawa.ko@gmail.com>
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

* Redistributions of source code must retain the above copyright notice, this
 list of conditions and the following disclaimer.

* Redistributions in binary form must reproduce the above copyright notice,
 this list of conditions and the following disclaimer in the documentation
  and/or other materials provided with the distribution.

* Neither the name of roguelike nor the names of its
 contributors may be used to endorse or promote products derived from
  this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED.IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/
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
    delegate void Use(Entity e);

    [Serializable]
    class Scroll : Entity
    {
        public override string name             // name property ... scrolldef で identified かどうか見て、nickname or realnameを返す 
        {
            get {
                bool identified = false;
                for (int i = 0; i < scrolldef.Count(); i++)
                {
                    if (scrolldef[i].realname == this.realname)
                    {
                        identified = scrolldef[i].identified;
                        break;
                    }
                }
                return ((identified) ? realname : nickname);
            }
            // set は、なし
        }

        // 識別されていない時のラベル（Scroll labeled Foo の Foo）。戦闘ビューで羊皮紙に書く
        public string label { get { return nickname.Replace("Scroll labeled ", ""); } }

        private Use myuse;                      // インスタンスごとの use() の実体をしまうところ
        private string nickname;                // 識別されていない時の名前
        private string realname;                // 識別済みの時の名前
        static private Random rnd = new Random();

        //
        // スクロールの諸元
        //
        private struct ScrollDefine
        {
            public string nickname;
            public string realname;
            public Use myuse;
            public bool identified;
            public bool harmful;

            public ScrollDefine (string nickname, Use myuse, string realname, bool harmful) {
                this.nickname  = nickname;
                this.myuse     = myuse;
                this.realname  = realname;
                this.identified = false;
                this.harmful   = harmful;
            }
        };
        static ScrollDefine[] scrolldef = new ScrollDefine[] {
                                       new ScrollDefine("Scroll labeled Foo", useIdentify,        "Scroll of Identify",       false),
                                       new ScrollDefine("Scroll labeled Bar", useEnchantWeapon,   "Scroll of Enchant Weapon", false),
                                       new ScrollDefine("Scroll labeled Baz", useEnchantArmor,    "Scroll of Enchant Armor",  false),
                                       new ScrollDefine("Scroll labeled XXX", useRustProofWeapon, "Scroll of Protect Weapon", false),
                                       new ScrollDefine("Scroll labeled YYY", useRustProofArmor,  "Scroll of Protect Armor",  false),
                                       new ScrollDefine("Scroll labeled ZZZ", useSleep,           "Scroll of Sleep",          true),
                                   };

        // スタティックコンストラクタで、nickname をシャッフルしておく
        static Scroll()
        {
            Random rnd = new Random();
            for (int i = 0; i < scrolldef.Count() * 3; i++)
            {
                int j = rnd.Next(scrolldef.Count());
                int k = rnd.Next(scrolldef.Count());
                if (i != k)
                {
                    string tmp = scrolldef[j].nickname;
                    scrolldef[j].nickname = scrolldef[k].nickname;
                    scrolldef[k].nickname = tmp;
                }
            } 
        }

        public Scroll(MazeAlgo maze, int floor)
            : base(maze)
        {
            graph = graphOrig = '?';

            int i = rnd.Next(scrolldef.Count());   // scrolldef の番号（均等に抽選）

            nickname = scrolldef[i].nickname;
            myuse    = scrolldef[i].myuse;
            realname = scrolldef[i].realname;
        }

        //
        // use メソッド群
        //
        static public void useIdentify(Entity user)
        {
            // 未識別の物から1個選んで…
            List<Entity> nonIdentified = new List<Entity>();
            foreach (Item i in user.itemlist)
            {
                if (i.entity.isIdentified() == false)
                {
                    nonIdentified.Add(i.entity);
                }
            }
            // …識別する
            if (nonIdentified.Count() != 0)
            {
                int i = rnd.Next(nonIdentified.Count());
                nonIdentified[i].identify(user);
                Console.WriteLine("何かがわかった気がする");
                CombatLog.SetUseEffect(user, UseEffect.Identify, true);
            }
            else
            {
                Console.WriteLine("損した気分がした");
                CombatLog.SetUseEffect(user, UseEffect.Identify, false);
            }
        }

        static public void useEnchantWeapon(Entity user)
        {
            if (user.weapon == null)
            {
                Console.WriteLine("損した気がする");
                CombatLog.SetUseEffect(user, UseEffect.EnchantWeapon, false);
                return;
            }
            user.weapon.enchant();
            Console.WriteLine("武器が青白く輝いた");
            CombatLog.SetUseEffect(user, UseEffect.EnchantWeapon, true);
        }

        static public void useEnchantArmor(Entity user)
        {
            if (user.armor == null)
            {
                Console.WriteLine("損した気がする");
                CombatLog.SetUseEffect(user, UseEffect.EnchantArmor, false);
                return;
            }
            user.armor.enchant();
            Console.WriteLine("鎧が青白く輝いた");
            CombatLog.SetUseEffect(user, UseEffect.EnchantArmor, true);
        }

        static public void useRustProofWeapon(Entity user)
        {
            if (user.weapon == null)
            {
                Console.WriteLine("損した気がする");
                CombatLog.SetUseEffect(user, UseEffect.ProtectWeapon, false);
                return;
            }
            user.weapon.protect();
            Console.WriteLine("武器が金色に輝いた");
            CombatLog.SetUseEffect(user, UseEffect.ProtectWeapon, true);
        }

        static public void useRustProofArmor(Entity user)
        {
            if (user.armor == null)
            {
                Console.WriteLine("損した気がする");
                CombatLog.SetUseEffect(user, UseEffect.ProtectArmor, false);
                return;
            }
            user.armor.protect();
            Console.WriteLine("鎧が金色に輝いた");
            CombatLog.SetUseEffect(user, UseEffect.ProtectArmor, true);
        }

        static public void useSleep(Entity user)
        {
            user.frozen = 4 + rnd.Next(4);                  // rogue を参考に、4-8ターン寝る
            user.frozenBy = null;                           // 眠りでは凍傷を負わない
            Console.WriteLine("眠たい");
            CombatLog.SetUseEffect(user, UseEffect.Sleep, true);
        }

        //
        // Entity としての性質 (TODO: Potion と共通化できる)
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

            if (!user.suppressConsole)
                Console.WriteLine("{0} は {1} を拾った", user.name, this.name);
        }

        public override bool isIdentified()
        {
            for (int i = 0; i <scrolldef.Count(); i++)
            {
                if (scrolldef[i].realname == this.realname)
                {
                    return scrolldef[i].identified;
                }
            }
            Debug.Assert(false, "should return before here");
            return false;
        }

        public override bool isHarmful()
        {
            if (!isIdentified()) return false;  // 未識別なら害か不明
            for (int i = 0; i < scrolldef.Count(); i++)
            {
                if (scrolldef[i].realname == this.realname)
                    return scrolldef[i].harmful;
            }
            return false;
        }

        public override void identify(Entity user)              // scrolldef[].identified = true にすることで、name プロパティが realname を返すようになる
        {
            for (int i = 0; i < scrolldef.Count(); i++)
            {
                if (scrolldef[i].realname == this.realname)
                {
                    scrolldef[i].identified = true;
                    return;
                }
            }
            Debug.Assert(false, "should return before here");;
        }
    }
}
