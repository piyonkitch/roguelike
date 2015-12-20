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

using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Maze
{
    [Serializable]
    class Entity
    {
        public int xpos { get; set;}
        public int ypos { get; set;}        // 座標
        public char graph { get; set; }     // "@" とか
        public char graphOrig { get; set; } // バックアップ
        public virtual string name { get; set; }    // 名前
        public int hit { get; set; }        // ヒットポイント 
        public int hitmax { get; set; }     // ヒットポイント（最大） 
        public int strength { get; set; }   // 強さ
        public int strengthmax { get; set; }// 強さ（最大）
        public int toughness { get; set; }  // 耐久性
        public int gold { get; set; }       // ゴールド 
        public int experience { get; set; } // 経験値
        public bool amnesia { get; set; }   // 物忘れ症
        public bool levitation { get; set; }// 浮遊
        public int frozen { get; set; }     // 凍っているターン数
        public List<Item> itemlist;         // 持ち物リスト
        public Weapon weapon { get; set; }  // 武器 XXX public である必要があるか？
        public Armor armor { get; set; }    // 鎧 XXX public である必要があるか？

        Random rnd = new Random();

        public Entity(MazeAlgo maze)
        {
            do
            {
                xpos = rnd.Next(Constant.NGRID);
                ypos = rnd.Next(Constant.NGRID);
            } while (maze.isWall(xpos, ypos));
            hit = hitmax = 3;
            experience = 0;
            strength = strengthmax = 1;
            toughness = 0;
            graph = graphOrig = '?';
            name = "Noname";
            itemlist = new List<Item>();
        }

        public Entity Clone()
        {
            return (Entity)MemberwiseClone();
        }

        public void changePos(MazeAlgo maze)        // 壁ではないところに配置
        {
            do
            {
                xpos = rnd.Next(Constant.NGRID);
                ypos = rnd.Next(Constant.NGRID);
            } while (maze.isWall(xpos, ypos));
        }

        private bool tryMove(int x, int y, MazeAlgo maze, List<Entity> entitylist)
        {
            if (maze.isWall(x, y)) return false;

            List<Entity> thigsOnGrid = new List<Entity>(); 
            foreach (Entity e in entitylist)
            {
                if ((e.xpos == x) && (e.ypos == y)) // 誰かいる
                {
                    if (e.graph == ' ' ||           // 敵か物がなくなった、空間
                        e.graph == '%' ||           // 食べ物
                        e.graph == '$' ||           // ゴールド
                        e.graph == ')' ||           // 武器
                        e.graph == '[' ||           // 鎧
                        e.graph == '!' ||           // ポーション
                        e.graph == '?')             // 巻物
                    {
                        thigsOnGrid.Add(e);
                    }
                    else if (e.graph == '>')             // 下への階段
                    {
                        ;
                    }
                    else
                    {
                        //
                        // 上記以外は、生きている敵の処理
                        //
                        e.beat(this);                   // 攻撃していることを相手に伝える
                        //
                        // ヒットポイントの変化、経験値の変化は、ここから下に書く
                        //
                        // 自分の強さ - 相手の耐久性分、相手のヒットを減らす
                        int diff = this.getStrength() - e.getToughness();
                        if (diff > 0)
                        {
                            e.hit -= diff;
                            Console.WriteLine("{0} は {1} にヒット", this.name, e.name);
                        }
                        else
                        {
                            Console.WriteLine("{0} は {1} を攻撃したが、はじき返された", this.name, e.name);
                        }

                        if (e.hit <= 0)
                        {
                            e.graph = '%';              // 死体に変える
                            Console.WriteLine("{0} は {1} を倒した", this.name, e.name);
                            // 死んだ生物の持ち物を床に落とす準備 (床への配置は、Logic で行う)
                            foreach (Item i in e.itemlist)
                            {
                                i.entity.xpos = e.xpos; // 物のX,Yには、拾われた場所が入っているので、生物のX,Yで更新する
                                i.entity.ypos = e.ypos;
                                i.entity.graph = i.entity.graphOrig; // 例：' ' から '!' に戻す
                            }

                            experience++;
                            if (experience >= 5)
                            {
                                hitmax += rnd.Next(3) + 1;
                                experience = 0;
                            }
                        }
                        return false;                   // 生物がいるところは通れない
                    }
                }
            }

            // 拾えるものを拾う (浮遊していなければ)
            if (!levitation)
            {
                foreach (Entity e in thigsOnGrid)
                {
                    e.pickup(this);
                    e.graph = ' ';
                }
            }

            return true; // 誰もいない
        }

        public virtual void move(MazeAlgo maze, List<Entity> entitylist, Entity target)         // target へ向かって移動する
        { 
            ;           // 物やheroは、tick()では移動しない
        }

        public void manualmove(string dir, MazeAlgo maze, List<Entity> entitylist)
        {
            // 生きていなかったら、動けない
            if (!isLive()) return;
            // 凍ってたら、動けない
            if (frozen > 0) return;

            switch (dir) {
                case "←": { if (xpos > 0                  && tryMove(xpos-1, ypos, maze, entitylist)) xpos--; break; }
                case "→": { if (xpos < Constant.NGRID - 1 && tryMove(xpos+1, ypos, maze, entitylist)) xpos++; break; }
                case "↑": { if (ypos > 0                  && tryMove(xpos, ypos-1, maze, entitylist)) ypos--; break; }
                case "↓": { if (ypos < Constant.NGRID - 1 && tryMove(xpos, ypos+1, maze, entitylist)) ypos++; break; }
            }

            // 20% の確率でヒットを回復
            if (rnd.Next(100) < 20 && hit < hitmax) hit++;
        }

        public virtual bool isUsable()
        {
            return false;                           // 通常のものは何も役に立たない
        }

        public virtual void use(Entity user)        // user が使う
        {
            ;                                       // 通常のものは何も役に立たない
        }

        public virtual bool isWieldable()
        {
            return false;                           // 通常のものは武器ではない
        }

        public virtual void wield(Entity user)      // user が構える
        {
            ;                                       // 通常のものは武器ではない
        }

        public virtual void takeOffWeapon()
        {
            if (weapon != null)
            {
                weapon = null;
                Console.WriteLine("{0} は武器を外した", name);
            }
            else
            {
                Console.WriteLine("{0} は外す武器がない", name);
            }
        }

        public virtual bool isWearable()
        {
            return false;                           // 通常のものは鎧ではない
        }

        public virtual void wear(Entity user)       // user が着る
        {
            ;                                       // 通常のものは鎧ではない
        }

        public virtual bool isIdentified()
        {
            return true;                            // 通常のものは識別済み
        }

        public virtual void identify(Entity user)   // user が識別する
        {
            ;                                       // 通常のものは識別済み
        }

        public virtual void takeOffArmor()
        {
            if (armor != null)
            {
                armor = null;
                Console.WriteLine("{0} は鎧を脱いだ", name);
            }
            else
            {
                Console.WriteLine("{0} は脱ぐものがない", name);
            }
        }

        public int getStrength()
        {
            if (weapon != null) return (weapon.sharpness + strength);
            return strength;
        }

        public int getToughness()
        {
            if (armor != null) return (armor.hardness + toughness);
            return toughness;
        }
        
        public virtual void pickup(Entity user)     // user が拾う
        {
            if (graph == '%')
            {
                if (user.hit < user.hitmax) user.hit++;    // 食べて hit を増やす 
                Console.WriteLine("{0} は {1} を食べた", user.name, name);
            } 
        }

        public virtual void beat(Entity attacker)   // attacker が殴る
        {
            ;                                       // 通常のものは殴られても何も変わらない
        }

        public virtual void rust()                  // 錆びさせる
        {
            ;                                       // 通常のものは錆びない
        }

        public virtual void enchant()               // 魔法でエンチャントする
        {
            ;                                       // 通常の物はエンチャントできない
        }

        public virtual void protect()               // 魔法で錆び防止する
        {
            ;                                       // 通常の物はさびない
        }

        protected bool isLive()
        {
            if (hit <= 0) return false;
            return true;
        }
    }
}
