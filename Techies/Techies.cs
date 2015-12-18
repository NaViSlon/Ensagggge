namespace Techies
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using Ensage;
    using Ensage.Common;
    using Ensage.Common.Extensions;
    using Ensage.Common.Menu;

    using SharpDX;
    using SharpDX.Direct3D9;

    internal class Techies
    {
        #region Static Fields

        private static readonly Menu Menu = new Menu("#TECHIES", "techies", true, "npc_dota_hero_techies", true);

        private static readonly Dictionary<ClassID, double> RemoteMinesHeroDmg = new Dictionary<ClassID, double>();

        private static bool aghanims;

        private static Dictionary<ClassID, bool> enabledHeroes = new Dictionary<ClassID, bool>();

        private static Ability forceStaff;

        private static Dictionary<ClassID, double[]> heroTopPanel = new Dictionary<ClassID, double[]>();

        private static Ability landMines;

        private static float landMinesDmg;

        private static Dictionary<ClassID, double> landMinesHeroDmg = new Dictionary<ClassID, double>();

        private static uint landMinesLevel;

        private static bool loaded;

        private static Hero me;

        private static float monitor;

        private static Font panelText;

        private static IEnumerable<Player> players;

        //private static IEnumerable<Hero> enemyHeroes;

        private static Ability remoteMines;

        private static Dictionary<Unit, float> remoteMinesDb = new Dictionary<Unit, float>();

        private static float remoteMinesDmg;

        private static uint remoteMinesLevel;

        private static float remoteMinesRadius;

        private static Ability suicideAttack;

        private static float suicideAttackDmg;

        private static uint suicideAttackLevel;

        private static float suicideAttackRadius;

        private static Font suicideDmgText;

        private static Dictionary<ClassID, float> suicideHeroDmg = new Dictionary<ClassID, float>();

        #endregion

        #region Public Methods and Operators

        public static void Init()
        {
            var optionsMenu = new Menu("Options", "options");
            var detonationMenu = new Menu("Auto Detonation", "autoDetonation");
            detonationMenu.AddItem(new MenuItem("autoDetonate", "Detonate on heroes").SetValue(true));
            detonationMenu.AddItem(new MenuItem("autoDetonateCreeps", "Detonate on creeps").SetValue(true));
            optionsMenu.AddSubMenu(detonationMenu);
            var forceStaffMenu = new Menu("Auto ForceStaff", "autoForceStaff");
            forceStaffMenu.AddItem(new MenuItem("useForceStaff", "Use ForceStaff").SetValue(true));
            forceStaffMenu.AddItem(new MenuItem("checkRotating", "Dont use on turning enemy").SetValue(true));
            forceStaffMenu.AddItem(
                new MenuItem("straightTime", "Minimum straight time (secs)").SetValue(new Slider(0, 0, 5))
                    .SetTooltip("Use force staff only on enemies who havent changed their direction X seconds"));
            optionsMenu.AddSubMenu(forceStaffMenu);
            var drawingMenu = new Menu("Drawings", "drawings");
            drawingMenu.AddItem(new MenuItem("drawTopPanel", "Draw TopPanel").SetValue(true));
            drawingMenu.AddItem(new MenuItem("drawSuicideKills", "Draw killability with Suicide").SetValue(true));
            var suicideMenu = new Menu("Auto Suicide", "autoSuicide");
            suicideMenu.AddItem(new MenuItem("autoSuicide", "Auto Suicide").SetValue(true));
            suicideMenu.AddItem(
                new MenuItem("HPTreshold", "HP treshold percent").SetValue(new Slider(100, 1))
                    .SetTooltip("Use Suicide only if Your health percent goes below specified treshold"));
            optionsMenu.AddSubMenu(drawingMenu);
            optionsMenu.AddSubMenu(suicideMenu);
            Menu.AddSubMenu(optionsMenu);
            Menu.AddToMainMenu();
            Game.OnUpdate += Game_OnUpdate;
            ObjectMgr.OnAddEntity += ObjectMgr_OnAddEntity;
            ObjectMgr.OnRemoveEntity += ObjectMgr_OnRemoveEntity;

            loaded = false;
            forceStaff = null;
            //enemyHeroes = null;
            players = null;
            remoteMinesDb = new Dictionary<Unit, float>();
            heroTopPanel = new Dictionary<ClassID, double[]>();
            landMinesHeroDmg = new Dictionary<ClassID, double>();
            suicideHeroDmg = new Dictionary<ClassID, float>();
            enabledHeroes = new Dictionary<ClassID, bool>();
            var screenSize = new Vector2(Drawing.Width, Drawing.Height);
            monitor = screenSize.X / 1600;
            var monitorY = screenSize.Y / 720;

            panelText = new Font(
                Drawing.Direct3DDevice9,
                new FontDescription
                {
                    FaceName = "Tahoma",
                    Height = (int)(18 * monitorY),
                    OutputPrecision = FontPrecision.Raster,
                    Quality = FontQuality.ClearTypeNatural,
                    CharacterSet = FontCharacterSet.Default,
                    Italic = false,
                    MipLevels = 0,
                    PitchAndFamily = FontPitchAndFamily.Swiss,
                    Weight = FontWeight.ExtraBold,
                    Width = (int)(5 * monitor)
                });
            suicideDmgText = new Font(
                Drawing.Direct3DDevice9,
                new FontDescription
                {
                    FaceName = "Tahoma",
                    Height = (int)(13 * monitorY),
                    OutputPrecision = FontPrecision.Raster,
                    Quality = FontQuality.ClearTypeNatural,
                    CharacterSet = FontCharacterSet.Hangul,
                    Italic = false,
                    MipLevels = 3,
                    PitchAndFamily = FontPitchAndFamily.Modern,
                    Weight = FontWeight.Heavy,
                    Width = (int)(5 * monitor)
                });

            Drawing.OnPreReset += Drawing_OnPreReset;
            Drawing.OnPostReset += Drawing_OnPostReset;
            Drawing.OnEndScene += Drawing_OnEndScene;
            AppDomain.CurrentDomain.DomainUnload += CurrentDomainDomainUnload;
            Game.OnWndProc += Game_OnWndProc;
        }

        #endregion

        #region Methods

        private static void CurrentDomainDomainUnload(object sender, EventArgs e)
        {
            panelText.Dispose();
            suicideDmgText.Dispose();
        }

        private static void Drawing_OnEndScene(EventArgs args)
        {
            if (Drawing.Direct3DDevice9 == null || Drawing.Direct3DDevice9.IsDisposed || !Game.IsInGame || me == null
                || (!Menu.Item("drawTopPanel").GetValue<bool>() && !Menu.Item("drawSuicideKills").GetValue<bool>()))
            {
                return;
            }

            //Console.WriteLine(players.Count());
            try
            {
                if (players == null || players.Count() < 5)
                {
                    players =
                        ObjectMgr.GetEntities<Player>()
                            .Where(x => x != null && x.Hero != null && x.Hero.Team == me.GetEnemyTeam());
                }
                var enumerable = players as Player[] ?? players.ToArray();
                //Console.WriteLine(enumerable.Count());
                foreach (var hero in
                    enumerable.Select(x => x.Hero))
                {
                    var classId = hero.ClassID;
                    bool enabled;
                    if (!enabledHeroes.TryGetValue(classId, out enabled))
                    {
                        enabledHeroes[classId] = true;
                    }
                    var health = hero.Health;
                    if (!hero.IsAlive)
                    {
                        health = hero.MaximumHealth;
                    }
                    double[] topPanel;
                    if (!heroTopPanel.TryGetValue(classId, out topPanel))
                    {
                        topPanel = new double[3];
                        topPanel[0] = HUDInfo.GetTopPanelSizeX(hero);
                        topPanel[1] = HUDInfo.GetTopPanelPosition(hero).X;
                        topPanel[2] = HUDInfo.GetTopPanelSizeY(hero) * 1.4;
                        heroTopPanel.Add(classId, topPanel);
                    }
                    var sizeX = topPanel[0];
                    var x = topPanel[1];
                    var sizey = topPanel[2];
                    if (Menu.Item("drawTopPanel").GetValue<bool>())
                    {
                        if (remoteMinesDmg > 0)
                        {
                            double remoteNumber;
                            if (!RemoteMinesHeroDmg.TryGetValue(classId, out remoteNumber))
                            {
                                remoteNumber =
                                    Math.Ceiling(health / hero.DamageTaken(remoteMinesDmg, DamageType.Magical, me));
                                RemoteMinesHeroDmg.Add(classId, remoteNumber);
                                Utils.Sleep(1000, classId + " remoteNumber");
                            }
                            else if (Utils.SleepCheck(classId + " remoteNumber"))
                            {
                                remoteNumber =
                                    Math.Ceiling(health / hero.DamageTaken(remoteMinesDmg, DamageType.Magical, me));
                                RemoteMinesHeroDmg[classId] = remoteNumber;
                                Utils.Sleep(1000, classId + " remoteNumber");
                            }
                            panelText.DrawText(
                                null,
                                remoteNumber.ToString(CultureInfo.InvariantCulture),
                                (int)(x + sizeX / 3.6),
                                (int)sizey,
                                enabled ? Color.Green : Color.DimGray);
                        }
                        if (landMinesDmg > 0)
                        {
                            double landNumber;
                            if (!landMinesHeroDmg.TryGetValue(classId, out landNumber))
                            {
                                landNumber =
                                    Math.Ceiling(health / hero.DamageTaken(landMinesDmg, DamageType.Physical, me));
                                landMinesHeroDmg.Add(classId, landNumber);
                                Utils.Sleep(1000, classId + " remoteNumber");
                            }
                            else if (Utils.SleepCheck(classId + " remoteNumber"))
                            {
                                landNumber =
                                    Math.Ceiling(health / hero.DamageTaken(landMinesDmg, DamageType.Physical, me));
                                landMinesHeroDmg[classId] = landNumber;
                                Utils.Sleep(1000, classId + " remoteNumber");
                            }
                            panelText.DrawText(
                                null,
                                landNumber.ToString(CultureInfo.InvariantCulture),
                                (int)x,
                                (int)sizey,
                                enabled ? Color.Red : Color.DimGray);
                        }
                    }
                    if (suicideAttackDmg > 0)
                    {
                        float dmg;

                        if (!suicideHeroDmg.TryGetValue(classId, out dmg))
                        {
                            dmg = health - hero.DamageTaken(suicideAttackDmg, DamageType.Physical, me);
                            suicideHeroDmg.Add(classId, dmg);
                            Utils.Sleep(150, classId + " canKill");
                        }
                        else if (Utils.SleepCheck(classId + " canKill"))
                        {
                            dmg = health - hero.DamageTaken(suicideAttackDmg, DamageType.Physical, me);
                            suicideHeroDmg[classId] = dmg;
                            Utils.Sleep(150, classId + " canKill");
                        }
                        var canKill = dmg <= 0;
                        if (Menu.Item("drawTopPanel").GetValue<bool>())
                        {
                            panelText.DrawText(
                                null,
                                canKill ? "Yes" : "No",
                                canKill ? (int)(x + sizeX / 2) : (int)(x + sizeX / 1.7),
                                (int)sizey,
                                enabled ? Color.DarkOrange : Color.DimGray);
                        }
                        if (!hero.IsVisible || !hero.IsAlive)
                        {
                            continue;
                        }
                        if (Menu.Item("drawSuicideKills").GetValue<bool>())
                        {
                            var screenPos = HUDInfo.GetHPbarPosition(hero);
                            if (screenPos.X + 20 > Drawing.Width || screenPos.X - 20 < 0
                                || screenPos.Y + 100 > Drawing.Height || screenPos.Y - 30 < 0)
                            {
                                continue;
                            }
                            suicideDmgText.DrawText(
                                null,
                                canKill ? "Yes" : "No " + Math.Floor(dmg),
                                (int)(screenPos.X),
                                (int)(screenPos.Y - HUDInfo.GetHpBarSizeY(hero) * 1.5),
                                enabled ? (canKill ? Color.LawnGreen : Color.Red) : Color.Gray);
                        }
                    }
                }
            }
            catch (Exception)
            {
                players =
                    ObjectMgr.GetEntities<Player>()
                        .Where(x => x != null && x.Hero != null && x.Hero.Team == me.GetEnemyTeam());
            }
        }

        private static void Drawing_OnPostReset(EventArgs args)
        {
            panelText.OnResetDevice();
            suicideDmgText.OnResetDevice();
        }

        private static void Drawing_OnPreReset(EventArgs args)
        {
            panelText.OnLostDevice();
            suicideDmgText.OnLostDevice();
        }

        private static Dictionary<int, Ability> FindDetonatableBombs(
            Unit hero,
            Vector3 pos,
            IEnumerable<KeyValuePair<Unit, float>> bombs)
        {
            var possibleBombs = bombs.Where(x => x.Key.Distance2D(pos) <= remoteMinesRadius);
            var detonatableBombs = new Dictionary<int, Ability>();
            var dmg = 0f;
            foreach (var bomb in possibleBombs)
            {
                if (dmg > 0)
                {
                    var takenDmg = hero.DamageTaken(dmg, DamageType.Magical, me);
                    if (takenDmg >= hero.Health)
                    {
                        break;
                    }
                }
                detonatableBombs[detonatableBombs.Count + 1] = bomb.Key.Spellbook.Spell1;
                dmg += bomb.Value;
            }
            dmg = hero.DamageTaken(dmg, DamageType.Magical, me);
            return dmg < hero.Health ? null : detonatableBombs;
        }

        private static void Game_OnUpdate(EventArgs args)
        {
            if (!loaded)
            {
                me = ObjectMgr.LocalHero;
                if (!Game.IsInGame || me == null || me.ClassID != ClassID.CDOTA_Unit_Hero_Techies)
                {
                    return;
                }
                loaded = true;
                remoteMines = me.Spellbook.SpellR;
                suicideAttack = me.Spellbook.SpellE;
                landMines = me.Spellbook.SpellQ;
                forceStaff = null;
                //enemyHeroes = null;
                players = null;
                remoteMinesDb = new Dictionary<Unit, float>();
                heroTopPanel = new Dictionary<ClassID, double[]>();
                landMinesHeroDmg = new Dictionary<ClassID, double>();
                suicideHeroDmg = new Dictionary<ClassID, float>();
                enabledHeroes = new Dictionary<ClassID, bool>();
                var screenSize = new Vector2(Drawing.Width, Drawing.Height);
                monitor = screenSize.X / 1280;
                if (me.AghanimState())
                {
                    var firstOrDefault = remoteMines.AbilityData.FirstOrDefault(x => x.Name == "damage_scepter");
                    if (firstOrDefault != null)
                    {
                        remoteMinesDmg = firstOrDefault.GetValue(remoteMines.Level - 1);
                    }
                }
                else
                {
                    var firstOrDefault = remoteMines.AbilityData.FirstOrDefault(x => x.Name == "damage");
                    if (firstOrDefault != null)
                    {
                        remoteMinesDmg = firstOrDefault.GetValue(remoteMines.Level - 1);
                    }
                }
                foreach (var bomb in
                    ObjectMgr.GetEntities<Unit>()
                        .Where(
                            x =>
                            x.ClassID == ClassID.CDOTA_NPC_TechiesMines && x.Spellbook.Spell1 != null
                            && x.Spellbook.Spell1.IsValid && x.Spellbook.Spell1.CanBeCasted() && x.IsAlive)
                        .Where(bomb => !remoteMinesDb.ContainsKey(bomb)))
                {
                    remoteMinesDb.Add(bomb, remoteMinesDmg);
                    if (bomb.Health < bomb.Health/2)
                    {
                        bomb.Spellbook.SpellQ.UseAbility();
                    }
                }
                //enemyHeroes =
                //    ObjectMgr.GetEntities<Hero>()
                //        .Where(x => x != null && x.IsValid && x.Team == me.GetEnemyTeam() && !x.IsIllusion);
                players =
                    ObjectMgr.GetEntities<Player>()
                        .Where(x => x != null && x.Hero != null && x.Hero.Team == me.GetEnemyTeam());
                Game.PrintMessage(
                    "<font face='Tahoma'><font color='#993311'>#TECHIES</font> by MOON<font color='#ff9900'>ES</font> loaded!</font> ",
                    MessageType.LogMessage);
            }
            if (!Game.IsInGame || me == null || me.ClassID != ClassID.CDOTA_Unit_Hero_Techies)
            {
                loaded = false;
                return;
            }

            if (Game.IsPaused)
            {
                return;
            }

            if (forceStaff == null && Menu.Item("useForceStaff").GetValue<bool>()
                )
            {
                forceStaff = me.Inventory.Items.FirstOrDefault(x => x.ClassID == ClassID.CDOTA_Item_ForceStaff);
            }
            var detonate = ObjectMgr.GetEntities<Unit>()
                .Where(x => x.ClassID == ClassID.CDOTA_NPC_TechiesMines && x.Spellbook.Spell1 != null&& x.Spellbook.Spell1.IsValid && x.Spellbook.Spell1.CanBeCasted() && x.Health <= (x.MaximumHealth * 0.6) && x.IsAlive).FirstOrDefault();
            if (detonate != null && Utils.SleepCheck(detonate.Handle.ToString()))
            {
                detonate.Spellbook.SpellQ.UseAbility();
                Utils.Sleep(400, detonate.Handle.ToString());

            }

            var suicideLevel = suicideAttack.Level;

            if (suicideAttackLevel != suicideLevel)
            {
                var firstOrDefault = suicideAttack.AbilityData.FirstOrDefault(x => x.Name == "damage");
                if (firstOrDefault != null)
                {
                    suicideAttackDmg = firstOrDefault.GetValue(suicideLevel - 1);
                }
                var abilityData = suicideAttack.AbilityData.FirstOrDefault(x => x.Name == "small_radius");
                if (abilityData != null)
                {
                    suicideAttackRadius = abilityData.Value;
                }
                suicideAttackLevel = suicideLevel;
            }

            var bombLevel = remoteMines.Level;

            if (remoteMinesLevel != bombLevel)
            {
                if (me.AghanimState())
                {
                    var firstOrDefault = remoteMines.AbilityData.FirstOrDefault(x => x.Name == "damage_scepter");
                    if (firstOrDefault != null)
                    {
                        remoteMinesDmg = firstOrDefault.GetValue(bombLevel - 1);
                    }
                }
                else
                {
                    var firstOrDefault = remoteMines.AbilityData.FirstOrDefault(x => x.Name == "damage");
                    if (firstOrDefault != null)
                    {
                        remoteMinesDmg = firstOrDefault.GetValue(bombLevel - 1);
                    }
                }
                var abilityData = remoteMines.AbilityData.FirstOrDefault(x => x.Name == "radius");
                if (abilityData != null)
                {
                    remoteMinesRadius = abilityData.Value + 20;
                }
                remoteMinesLevel = bombLevel;
            }

            var landMineslvl = landMines.Level;

            if (landMinesLevel != landMineslvl)
            {
                var firstOrDefault = landMines.AbilityData.FirstOrDefault(x => x.Name == "damage");
                if (firstOrDefault != null)
                {
                    landMinesDmg = firstOrDefault.GetValue(landMineslvl - 1);
                }
                landMinesLevel = landMineslvl;
            }

            if (!aghanims && me.AghanimState())
            {
                var firstOrDefault = remoteMines.AbilityData.FirstOrDefault(x => x.Name == "damage_scepter");
                if (firstOrDefault != null)
                {
                    remoteMinesDmg = firstOrDefault.GetValue(bombLevel - 1);
                }
                aghanims = true;
            }
            var bombs =
                remoteMinesDb.Where(
                    x =>
                    x.Key != null && x.Key.IsValid && x.Key.Spellbook.Spell1 != null
                    && x.Key.Spellbook.Spell1.CanBeCasted() && x.Key.IsAlive);
            var bombsArray = bombs as KeyValuePair<Unit, float>[] ?? bombs.ToArray();
            var eHeroes =
                ObjectMgr.GetEntities<Hero>()
                    .Where(
                        x =>
                        x != null && x.IsValid && !x.IsIllusion && x.Team == me.GetEnemyTeam() && x.IsAlive
                        && x.IsVisible && !x.IsMagicImmune()
                        && x.Modifiers.All(y => y.Name != "modifier_abaddon_borrowed_time")
                        && x.Modifiers.All(y => y.Name != "modifier_dazzle_shallow_grave")
                        && x.Modifiers.All(y => y.Name != "modifier_templar_assassin_refraction_absorb")

                        && Utils.SleepCheck(x.ClassID.ToString()));
            try
            {
                foreach (var hero in
                    eHeroes)
                {
                    bool enabled;
                    if (!enabledHeroes.TryGetValue(hero.ClassID, out enabled) || !enabled)
                    {
                        continue;
                    }
                    var heroDistance = me.Distance2D(hero);
                    if (Menu.Item("autoDetonate").GetValue<bool>())
                    {
                        var nearbyBombs = bombsArray.Any(x => x.Key.Distance2D(hero) <= remoteMinesRadius + 500);
                        if (nearbyBombs)
                        {
                            CheckBombDamageAndDetonate(hero, bombsArray);
                        }
                    }
                    //Game.PrintMessage((float)me.Health / me.MaximumHealth + " " + (float)Menu.Item("HPTreshold").GetValue<Slider>().Value / 100, MessageType.ChatMessage);
                    var zHeroes =
               ObjectMgr.GetEntities<Hero>()
                   .Where(
                       x =>
                       x != null && x.IsValid && !x.IsIllusion && x.Team == me.GetEnemyTeam() && x.IsAlive
                       && x.IsVisible && !x.IsMagicImmune()
                       && x.Modifiers.All(y => y.Name != "modifier_abaddon_borrowed_time")
                       && x.Modifiers.All(y => y.Name != "modifier_dazzle_shallow_grave")
                       && x.Modifiers.All(y => y.Name != "modifier_templar_assassin_refraction_absorb"));
                    foreach (var z in zHeroes)
                    {
                        if (Menu.Item("autoSuicide").GetValue<bool>() && suicideAttack.CanBeCasted()
                            && (float)me.Health / me.MaximumHealth
                            <= (float)Menu.Item("HPTreshold").GetValue<Slider>().Value / 100 && heroDistance < 400
                            && suicideAttackLevel > 0 && me.IsAlive
                            && z.Modifiers.All(y => y.Name != "modifier_dazzle_shallow_grave"))
                        {
                            SuicideKillSteal(hero);
                        }
                    }
                    if (forceStaff == null || !Menu.Item("useForceStaff").GetValue<bool>()
                        || !(heroDistance <= forceStaff.CastRange) || !Utils.SleepCheck("forcestaff")
                        || bombsArray.Any(x => x.Key.Distance2D(hero) <= remoteMinesRadius)
                        || Prediction.IsTurning(hero) || !forceStaff.CanBeCasted())
                    {
                        continue;
                    }

                    double rotSpeed;
                    if (!Prediction.RotSpeedDictionary.TryGetValue(hero.Handle, out rotSpeed)
                        || (rotSpeed > 0 && Menu.Item("checkRotating").GetValue<bool>()))
                    {
                        continue;
                    }
                    if (Prediction.StraightTime(hero) / 1000 < Menu.Item("straightTime").GetValue<Slider>().Value)
                    {
                        continue;
                    }
                    var turnTime = me.GetTurnTime(hero);
                    var forcePosition = hero.Position;
                    if (hero.NetworkActivity == NetworkActivity.Move)
                    {
                        forcePosition = Prediction.InFront(
                            hero,
                            (float)((turnTime + Game.Ping / 1000) * hero.MovementSpeed));
                    }
                    forcePosition +=
                        VectorExtensions.FromPolarCoordinates(1f, (float)(hero.NetworkRotationRad + rotSpeed))
                            .ToVector3() * 600;
                    var possibleBombs = bombsArray.Any(x => x.Key.Distance2D(forcePosition) <= (remoteMinesRadius - 75));
                    if (!possibleBombs)
                    {
                        continue;
                    }
                    var dmg = CheckBombDamage(hero, forcePosition, bombsArray);
                    if (!(dmg >= hero.Health) && hero.Modifiers.All(y => y.Name != "modifier_dazzle_shallow_grave" &&  y.Name != "modifier_templar_assassin_refraction_absorb"))
                    {
                        continue;
                    }
                    forceStaff.UseAbility(hero);
                    Utils.Sleep(250, "forcestaff");
                }
            }
            catch (Exception)
            {
                //aa
            }

            if (!Menu.Item("autoDetonateCreeps").GetValue<bool>())
            {
                return;
            }
            var creeps =
                ObjectMgr.GetEntities<Creep>()
                    .Where(
                        x =>
                        (x.ClassID == ClassID.CDOTA_BaseNPC_Creep_Lane || x.ClassID == ClassID.CDOTA_BaseNPC_Creep_Siege)
                        && x.IsAlive && x.IsVisible && x.IsSpawned && x.Team == me.GetEnemyTeam());
            var enumerable = creeps as Creep[] ?? creeps.ToArray();

            foreach (var data in (from creep in enumerable
                                  let nearbyBombs =
                                      bombsArray.Any(x => x.Key.Distance2D(creep) <= remoteMinesRadius + 500)
                                  where nearbyBombs
                                  let detonatableBombs = FindDetonatableBombs(creep, creep.Position, bombsArray)
                                  where detonatableBombs != null
                                  let nearbyCreeps =
                                      enumerable.Count(
                                          x =>
                                          x.Distance2D(creep) <= remoteMinesRadius
                                          && CheckBombDamage(x, x.Position, bombsArray) >= x.Health)
                                  where nearbyCreeps > 3
                                  select detonatableBombs).SelectMany(
                                      detonatableBombs =>
                                      detonatableBombs.Where(data => Utils.SleepCheck(data.Value.Handle.ToString()))))
            {
                data.Value.UseAbility();
                Utils.Sleep(250, data.Value.Handle.ToString());
            }
        }

        private static void Game_OnWndProc(WndEventArgs args)
        {
            if (args.Msg != (ulong)Utils.WindowsMessages.WM_LBUTTONDOWN)
            {
                return;
            }
            try
            {
                foreach (var hero in
                    from play in players
                    select play.Hero
                        into hero
                        let sizeX = (float)HUDInfo.GetTopPanelSizeX(hero)
                        let x = HUDInfo.GetTopPanelPosition(hero).X
                        let sizey = HUDInfo.GetTopPanelSizeY(hero) * 1.4
                        where Utils.IsUnderRectangle(Game.MouseScreenPosition, x, 0, sizeX, (float)(sizey * 1.4))
                        select hero)
                {
                    bool enabled;
                    if (enabledHeroes.TryGetValue(hero.ClassID, out enabled))
                    {
                        enabledHeroes[hero.ClassID] = !enabled;
                    }
                }
            }
            catch (Exception)
            {
                //
            }
        }

        private static float CheckBombDamage(Unit hero, Vector3 pos, IEnumerable<KeyValuePair<Unit, float>> bombs)
        {
            var dmg =
                bombs.Where(x => x.Key.Distance2D(pos) <= remoteMinesRadius).Sum(possibleBomb => possibleBomb.Value);
            return hero.DamageTaken(dmg, DamageType.Magical, me);
        }

        private static void CheckBombDamageAndDetonate(Unit hero, KeyValuePair<Unit, float>[] bombs)
        {
            var pos = hero.Position;
            if (hero.NetworkActivity == NetworkActivity.Move)
            {
                pos = Prediction.InFront(hero, (Game.Ping / 1000) * hero.MovementSpeed);
            }
            CheckBombDamageAndDetonate(hero, pos, bombs);
        }

        private static void CheckBombDamageAndDetonate(
            Unit hero,
            Vector3 pos,
            IEnumerable<KeyValuePair<Unit, float>> bombs)
        {
            if (!Utils.SleepCheck(hero.ClassID.ToString()))
            {
                return;
            }
            if (hero.Modifiers.Any(y => y.Name == "modifier_abaddon_borrowed_time"))
            {
                return;
            }
            var pos1 = pos;
            var turning = !Prediction.IsTurning(hero);
            var possibleBombs =
                bombs.Where(
                    x =>
                    x.Key.Distance2D(pos1) <= remoteMinesRadius && x.Key.Distance2D(hero.Position) <= remoteMinesRadius
                    && ((remoteMinesRadius - x.Key.Distance2D(pos1)) / hero.MovementSpeed > (Game.Ping / 1000)
                        || hero.NetworkActivity == NetworkActivity.Idle)
                    && (!turning || x.Key.Distance2D(hero) < remoteMinesRadius - 20
                        || x.Key.Distance2D(pos1) - 10 < x.Key.Distance2D(hero)));
            var detonatableBombs = new Dictionary<Unit, Ability>();
            var dmg = 0f;
            foreach (var bomb in possibleBombs)
            {
                if (dmg > 0)
                {
                    var takenDmg = hero.DamageTaken(dmg, DamageType.Magical, me);
                    if (takenDmg >= hero.Health)
                    {
                        break;
                    }
                }
                detonatableBombs[bomb.Key] = bomb.Key.Spellbook.Spell1;
                dmg += bomb.Value;
            }
            dmg = hero.DamageTaken(dmg, DamageType.Magical, me);
            if (dmg < hero.Health)
            {
                return;
            }
            if (hero.NetworkActivity == NetworkActivity.Move)
            {
                pos = Prediction.InFront(hero, ((Game.Ping / 1000) * hero.MovementSpeed));
                var stop = false;
                foreach (var mine in
                    detonatableBombs.Where(data => Utils.SleepCheck(data.Value.Handle.ToString()))
                        .Select(data => data.Key)
                        .Where(
                            mine =>
                            mine.Distance2D(pos) > remoteMinesRadius
                            || ((remoteMinesRadius - mine.Distance2D(pos)) / hero.MovementSpeed
                                < (Game.Ping / 1000 + detonatableBombs.Count * 0.002)
                                && hero.NetworkActivity != NetworkActivity.Idle)))
                {
                    stop = true;
                }
                if (stop)
                {
                    return;
                }
            }
            foreach (var data in detonatableBombs.Where(data => Utils.SleepCheck(data.Value.Handle.ToString())))
            {
                data.Value.UseAbility();
                Utils.Sleep(250, data.Value.Handle.ToString());
            }
            Utils.Sleep(1000, hero.ClassID.ToString());
        }

        private static void ObjectMgr_OnAddEntity(EntityEventArgs args)
        {
            if (me == null || !Game.IsInGame)
            {
                return;
            }
            var ent = args.Entity as Unit;
            if (ent == null)
            {
                return;
            }
            //if (ent is Hero && !ent.IsIllusion && ent.Team == me.GetEnemyTeam())
            //{
            //    Console.WriteLine("Heroadded");
            //    enemyHeroes =
            //        ObjectMgr.GetEntities<Hero>()
            //            .Where(x => x != null && x.IsValid && x.Team == me.GetEnemyTeam() && !x.IsIllusion);
            //}
            if ((ent.ClassID == ClassID.CDOTA_NPC_TechiesMines) && !remoteMinesDb.ContainsKey(ent))
            {
                remoteMinesDb.Add(ent, remoteMinesDmg);
            }
        }

        private static void ObjectMgr_OnRemoveEntity(EntityEventArgs args)
        {
            if (me == null || !Game.IsInGame)
            {
                return;
            }
            var ent = args.Entity as Unit;
            if (ent != null && (ent.ClassID == ClassID.CDOTA_NPC_TechiesMines) && remoteMinesDb.ContainsKey(ent))
            {
                remoteMinesDb.Remove(ent);
            }
        }

        private static void SuicideKillSteal(Unit hero)
        {
            if (!Utils.SleepCheck("suicide"))
            {
                return;
            }
            var dmg = hero.DamageTaken(suicideAttackDmg, DamageType.Physical, me, true);
            //Console.WriteLine(dmg);
            if (!(dmg >= hero.Health))
            {
                return;
            }
            var pos = hero.NetworkPosition;
            if (hero.NetworkActivity == (NetworkActivity)1502)
            {
                pos = Prediction.InFront(hero, (float)((Game.Ping / 1000 + me.GetTurnTime(hero)) * hero.MovementSpeed));
            }
            if (pos.Distance2D(me) < hero.Distance2D(me))
            {
                pos = hero.Position;
            }
            if (!(pos.Distance2D(me) < suicideAttackRadius))
            {
                return;
            }
            if (me.Distance2D(pos) > 100)
            {
                pos = (pos - me.Position) * 99 / pos.Distance2D(me) + me.Position;
            }
            suicideAttack.UseAbility(pos);
            Utils.Sleep(500, "suicide");
        }

        #endregion
    }
}
