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
    class Logic
    {
        public MazeAlgo maze { get; set; }
        public Entity hero { get; set;  }
        public int floor { get; set;  }
        public List<Entity> entitylist { get; set; }

        // Form の RogueLike() から呼ばれる
        public void init()
        {
            floor = 1;

            maze = new MazeDist();
            maze.initmaze();

            entitylist = new List<Entity>();

            hero = new Hero(maze);
            entitylist.Add(hero);
            System.Threading.Thread.Sleep(20);

            initEnemyAndThings();               // entitylist に敵と物を配置

            // hero の周りだけ見えるようにする
            newvision();
        }

        public void initNextLevel()
        {
            floor++;

            maze = new MazeDist();
            maze.initmaze();
            entitylist = new List<Entity>();    // これで、前の階層の entitylist は忘れる

            entitylist.Add(hero);               // 既存のheroをentitylistに配置
            hero.changePos(maze);               // 壁以外のところへ移動
            initEnemyAndThings();               // entitylist に敵と物を配置

            // hero の周りだけ見えるようにする
            newvision();
        }

        private void initEnemyAndThings()
        {
            string   clist =     "ABCDEFGHIJKLMNOPQRSTUVWXYZ$[)!?>";
            string[] elist = {
                                 "03000000004000000000000000522941",
                                 "00000002204000200000000000522341",
                                 "00000002400000600000000000522341",
                                 "00060000000000200000000000522340",
                             };
            // AcidBlob
            for (int i = 0; i < int.Parse(elist[floor - 1].Substring(clist.IndexOf("A"), 1)); i++)
            {
                entitylist.Add(new Acid(maze));
                System.Threading.Thread.Sleep(20);
            }
            // Bat
            for (int i = 0; i < int.Parse(elist[floor - 1].Substring(clist.IndexOf("B"), 1)); i++)
            {
                entitylist.Add(new Bat(maze));
                System.Threading.Thread.Sleep(20);
            }
            // Dragon
            for (int i = 0; i < int.Parse(elist[floor - 1].Substring(clist.IndexOf("D"), 1)); i++)
            {
                entitylist.Add(new Dragon(maze));
                System.Threading.Thread.Sleep(20);
            }
            // Hobbit
            for (int i = 0; i < int.Parse(elist[floor - 1].Substring(clist.IndexOf("H"), 1)); i++)
            {
                entitylist.Add(new Hobbit(maze));
                System.Threading.Thread.Sleep(20);
            }
            // Ice
            for (int i = 0; i < int.Parse(elist[floor - 1].Substring(clist.IndexOf("I"), 1)); i++)
            {
                entitylist.Add(new Ice(maze));
                System.Threading.Thread.Sleep(20);
            }
            // Kobold
            for (int i = 0; i < int.Parse(elist[floor - 1].Substring(clist.IndexOf("K"), 1)); i++)
            {
                entitylist.Add(new Kobold(maze));
                System.Threading.Thread.Sleep(20);
            }
            // Orc
            for (int i = 0; i < int.Parse(elist[floor - 1].Substring(clist.IndexOf("O"), 1)); i++)
            {
                entitylist.Add(new Orc(maze));
                System.Threading.Thread.Sleep(20);
            }
            // Gold
            for (int i = 0; i < int.Parse(elist[floor - 1].Substring(clist.IndexOf("$"), 1)); i++)
            {
                entitylist.Add(new Gold(maze, floor));
                System.Threading.Thread.Sleep(20);
            }
            // Potion
            for (int i = 0; i < int.Parse(elist[floor - 1].Substring(clist.IndexOf("!"), 1)); i++)
            {
                entitylist.Add(new Potion(maze, floor));
                System.Threading.Thread.Sleep(20);
            }
            // Scroll
            for (int i = 0; i < int.Parse(elist[floor - 1].Substring(clist.IndexOf("?"), 1)); i++)
            {
                entitylist.Add(new Scroll(maze, floor));
                System.Threading.Thread.Sleep(20);
            }
            // Weapon
            for (int i = 0; i < int.Parse(elist[floor - 1].Substring(clist.IndexOf(")"), 1)); i++)
            {
                entitylist.Add(new Weapon(maze, floor));
                System.Threading.Thread.Sleep(20);
            }
            // Armor
            for (int i = 0; i < int.Parse(elist[floor - 1].Substring(clist.IndexOf("["), 1)); i++)
            {
                entitylist.Add(new Armor(maze, floor));
                System.Threading.Thread.Sleep(20);
            }
            // Stair
            for (int i = 0; i < int.Parse(elist[floor - 1].Substring(clist.IndexOf(">"), 1)); i++)
            {
                do
                {
                    entitylist.Add(new Stair(maze));
                    System.Threading.Thread.Sleep(20);
                } while (maze.walk(hero.xpos, hero.ypos, entitylist.Last().xpos, entitylist.Last().ypos) == "");
            }
        }

        public bool isSeeThru(int fromx, int fromy, int tox, int toy) {
            // 隣は、からなず見える
            if (Math.Abs(tox - fromx) <= 1 && Math.Abs(toy - fromy) <= 1)
            {
                return true;
            }

            if (fromx < tox && Math.Abs(tox - fromx) > Math.Abs(toy - fromy) /*Xの差のほうが大きい*/)
            { // 右側の視界
                for (int tmpx = fromx; tmpx < tox; tmpx++)
                {
                    int tmpy = (int)((double)(tmpx - fromx) * /*傾きΔy/Δx*/(double)(toy - fromy) / (tox - fromx) + fromy + .5);
                    if (maze.isWall(tmpx, tmpy)) return false; // 壁ならば視界探索終わり
                }
                return true;
            }
            else if (tox < fromx && Math.Abs(tox - fromx) > Math.Abs(toy - fromy)/*Xの差のほうが大きい*/)
            { // 左側の視界
                for (int tmpx = fromx; tox < tmpx; tmpx--)
                {
                    int tmpy = (int)((double)(tmpx - fromx) * /*傾きΔy/Δx*/(double)(toy - fromy) / (tox - fromx) + fromy + .5);
                    if (maze.isWall(tmpx, tmpy)) return false; // 壁ならば視界探索終わり
                }
                return true;
            }
            else if (fromy < toy)
            {
                // 下側の視界で、Yの差のほうが大きい
                for (int tmpy = fromy; tmpy < toy; tmpy++)
                {
                    int tmpx = (int)((double)(tmpy - fromy) * /*傾きΔx/Δy*/(double)(tox - fromx) / (toy - fromy) + fromx + .5);
                    if (maze.isWall(tmpx, tmpy)) return false; // 壁ならば視界探索終わり
                }
                return true;
            }
            else
            {
                // 上側の視界で、Yの差のほうが大きい
                for (int tmpy = fromy; toy < tmpy; tmpy--)
                {
                    int tmpx = (int)((double)(tmpy - fromy) * /*傾きΔx/Δy*/(double)(tox - fromx) / (toy - fromy) + fromx + .5);
                    if (maze.isWall(tmpx, tmpy)) return false; // 壁ならば視界探索終わり
                }
                return true;
            }
        }

        private void newvision() // hero の周りを見えるようにする
        {
            if (hero.amnesia)       // 忘れ薬
            {
                for (int y = 0; y < Constant.NGRID; y++)
                {
                    for (int x = 0; x < Constant.NGRID; x++)
                    {
                        if (maze.isVisible(x, y)) // 見えるところだけ描画
                            maze.Invisible(x, y);
                    }
                }
                hero.amnesia = false;
            }

            for (int visiondist = 1; visiondist <= Constant.VISION_DISTANCE; visiondist++)
            {
                for (int y = Math.Max(hero.ypos - visiondist, 0); y <= Math.Min(hero.ypos + visiondist, Constant.NGRID - 1); y++)
                {
                    for (int x = Math.Max(hero.xpos - visiondist, 0); x <= Math.Min(hero.xpos + visiondist, Constant.NGRID - 1); x++)
                    {
                        if (isSeeThru(hero.xpos, hero.ypos, x, y)) maze.Visible(x, y); 
                    }
                }
            }
        }

        private void visionAllGrid() // 全迷路を見えるようにする
        {
            for (int y = 0; y < Constant.NGRID; y++)
            {
                for (int x = 0; x < Constant.NGRID; x++)
                {
                    maze.Visible(x, y);
                }
            }
        }

        public bool isEntitySeeable(Entity e)
        {
            return (isSeeThru(hero.xpos, hero.ypos, e.xpos, e.ypos) &&
                    Math.Sqrt(Math.Pow(e.xpos - hero.xpos, 2) + Math.Pow(e.ypos - hero.ypos, 2)) <= Constant.VISION_DISTANCE);
        }

        public void tick()
        {

            do
            {                    // hero.frozen が > 0 なら繰り返す
                foreach (Entity e in entitylist)
                {
                    e.move(maze, entitylist, hero);
                }

                // ' ' になってしまった Entity を削る (他の Entity と重なった時にうまくいかないので)
                for (int i = entitylist.Count - 1; i >= 0; i--)
                {
                    Entity eDel = entitylist[i];
                    if (eDel.graph == ' ')
                    {
                        entitylist.Remove(eDel);
                    }
                }

                // 死体の持ち物を entitylist へ戻す
                List<Entity> dropThings = new List<Entity>();
                foreach (Entity e in entitylist)        // 持ち物を dropThings に列挙する
                {
                    if (e.graph == '%')
                    {
                        foreach (Item i in e.itemlist)
                        {
                            dropThings.Add(i.entity);
                        }
                        e.itemlist.Clear();
                        if (e.gold > 0) dropThings.Add(new Gold(maze, floor, e.gold, e.xpos, e.ypos)); // gold があれば '$' を作る
                    }
                }
                foreach (Entity e in dropThings)        // foreach(entitylist) の外で、entitylist に dropThigs を追加する
                {
                    entitylist.Add(e);
                }

                // 視界を更新
                newvision();
            } while (hero.frozen-- > 0);
        }

        //
        // ユーザ操作から呼ばれる処理
        //
        public void ctrlUp()
        {
            hero.manualmove("↑", maze, entitylist);
            tick();
        }

        public void ctrlLeft()
        {
            hero.manualmove("←", maze, entitylist);
            tick();
        }

        public void ctrlRight()
        {
            hero.manualmove("→", maze, entitylist);
            tick();
        }

        public void ctrlDown()
        {
            hero.manualmove("↓", maze, entitylist);
            tick();
        }

        public void ctrlStairDown()
        {
            bool stair = false;
            foreach (Entity e in entitylist)
            {
                if (e.xpos == hero.xpos && e.ypos == hero.ypos && e.graph == '>')
                {
                    stair = true;
                    break;
                }
            }
            if (stair)
            {
                initNextLevel();                    // entitylist の書き換えは、foreach 外でやる
            }
        }

        public void ctrlUse(int index)
        {
            hero.itemlist[index].use(hero);

            if (hero.itemlist[index].num == 0)
            {
                hero.itemlist.RemoveAt(index);
            }
        }

        public void ctrlDrop(int index)
        {
            if (hero.itemlist[index].drop(hero) == false) return; // 数を1個減らす 

            // Potion などでは、entity は1個にまとまってしまっている。
            // entity をコピーしてから、entitylist に置く必要がある。
            Entity e = hero.itemlist[index].entity.Clone();
            e.xpos = hero.xpos;
            e.ypos = hero.ypos;
            e.graph = e.graphOrig;
            entitylist.Add(e);

            if (hero.itemlist[index].num == 0) hero.itemlist.RemoveAt(index);
        }

        public void ctrlWield(int index)
        {
            hero.itemlist[index].wield(hero);

            if (hero.itemlist[index].num == 0)
            {
                hero.itemlist.RemoveAt(index);
            }
        }

        public void ctrlTakeOffWeapon()
        {
            hero.takeOffWeapon();
        }

        public void ctrlWear(int index)
        {
            hero.itemlist[index].wear(hero);

            if (hero.itemlist[index].num == 0)
            {
                hero.itemlist.RemoveAt(index);
            }
        }

        public void ctrlTakeOffArmor()
        {
            hero.takeOffArmor();
        }

        public void ctrlSave()
        {
            try
            {
                using (Stream stream = File.OpenWrite("roguelike.bin"))
                {
                    BinaryFormatter formatter = new BinaryFormatter();

                    formatter.Serialize(stream, maze);
                    formatter.Serialize(stream, floor);
                    formatter.Serialize(stream, entitylist);
                }
            }
            catch (System.IO.IOException ex)
            {
                Console.WriteLine("ファイルを開けませんでした");
                Console.WriteLine(ex.Message);
            }
            catch (System.UnauthorizedAccessException ex)
            {
                Console.WriteLine("ファイルの書き込み権限がありません");
                Console.WriteLine(ex.Message);
            }
        }

        public void ctrlLoad()
        {
            try{
                using (Stream stream = File.OpenRead("roguelike.bin"))
                {
                    BinaryFormatter formatter = new BinaryFormatter();

                    maze = (MazeDist)formatter.Deserialize(stream);
                    floor = (int)formatter.Deserialize(stream);
                    entitylist = (List<Entity>)formatter.Deserialize(stream);
                    hero = entitylist[0];
                }
            }
            catch (System.IO.IOException ex)
            {
                Console.WriteLine("ファイルを開けませんでした");
                Console.WriteLine(ex.Message);
            }
            catch (System.UnauthorizedAccessException ex)
            {
                Console.WriteLine("ファイルの読み込み権限がありません");
                Console.WriteLine(ex.Message);
            }
            

        }

    }
}
