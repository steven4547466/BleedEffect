using Exiled.API.Features;
using Exiled.Events.EventArgs;
using MEC;
using EPlayer = Exiled.API.Features.Player;
using System.Collections.Generic;
using System;

namespace BleedEffect.Handlers
{
    public class Player
    {
        public CoroutineHandle co;
        public Dictionary<int, int> beenShot = new Dictionary<int, int>();
        public Dictionary<int, int> bleeding = new Dictionary<int, int>();
        public List<DamageTypes.DamageType> allowedDamageTypes = new List<DamageTypes.DamageType>();
        public bool affectsScps = false;
        public List<RoleType> affectedScps = null;

        public void Init()
        {
            Config config = BleedEffect.Instance.Config;
            if (config.E11RifleEnabled) allowedDamageTypes.Add(DamageTypes.E11StandardRifle);
            if (config.LogicerEnabled) allowedDamageTypes.Add(DamageTypes.Logicer);
            if (config.MicroEnabled) allowedDamageTypes.Add(DamageTypes.MicroHid);
            if (config.Mp7Enabled) allowedDamageTypes.Add(DamageTypes.Mp7);
            if (config.P90Enabled) allowedDamageTypes.Add(DamageTypes.P90);
            if (config.UspEnabled) allowedDamageTypes.Add(DamageTypes.Usp);
            if (config.GrenadeEnabled) allowedDamageTypes.Add(DamageTypes.Grenade);
            if (config.SCP939Enabled) allowedDamageTypes.Add(DamageTypes.Scp939);
            if (config.SCP049_2Enabled) allowedDamageTypes.Add(DamageTypes.Scp0492);
            if (config.AffectsScps)
            {
                affectsScps = true;
                affectedScps = new List<RoleType>();
                if (config.Affects049) affectedScps.Add(RoleType.Scp049);
                if (config.Affects049_2) affectedScps.Add(RoleType.Scp0492);
                if (config.Affects096) affectedScps.Add(RoleType.Scp096);
                if (config.Affects106) affectedScps.Add(RoleType.Scp106);
                if (config.Affects173) affectedScps.Add(RoleType.Scp173);
                if (config.Affects939)
                {
                    affectedScps.Add(RoleType.Scp93953);
                    affectedScps.Add(RoleType.Scp93989);
                }
            }
        }
        public void OnHurting(HurtingEventArgs ev)
        {
            if (beenShot == null || bleeding == null) return;
            Log.Debug($"Player with id {ev.Target.Id} has taken damage from {ev.DamageType.name}.", BleedEffect.Instance.Config.Debug);
            if (!affectsScps && ev.Target.Team == Team.SCP) return;
            else if (ev.Target.Team == Team.SCP)
            {
                if (!affectedScps.Contains(ev.Target.Role)) return;
            }
            if (!allowedDamageTypes.Contains(ev.DamageType))
            {
                Log.Debug($"{ev.DamageType.name} has not passed allowed damage types.", BleedEffect.Instance.Config.Debug);
                return;
            }
            Log.Debug($"{ev.DamageType.name} has passed allowed damage types.", BleedEffect.Instance.Config.Debug);
            if (!beenShot.ContainsKey(ev.Target.Id)) beenShot.Add(ev.Target.Id, 1);
            else beenShot[ev.Target.Id] += 1;
            if (beenShot[ev.Target.Id] % BleedEffect.Instance.Config.ShotsToBleed == 0)
            {
                if (beenShot[ev.Target.Id] == BleedEffect.Instance.Config.ShotsToBleed && BleedEffect.Instance.Config.BleedMessage != "") ev.Target.Broadcast(5, $"<color=\"red\">{BleedEffect.Instance.Config.BleedMessage}</color>");
                if (beenShot[ev.Target.Id] > BleedEffect.Instance.Config.ShotsToBleed && BleedEffect.Instance.Config.IncreasedBleedMessage != "") ev.Target.Broadcast(5, $"<color=\"red\">{BleedEffect.Instance.Config.IncreasedBleedMessage}</color>");
                if (!bleeding.ContainsKey(ev.Target.Id)) bleeding.Add(ev.Target.Id, 1);
                else bleeding[ev.Target.Id] += 1;
                if (!BleedEffect.Instance.mainCoroEnabled)
                {
                    BleedEffect.Instance.mainCoroEnabled = true;
                    co = Timing.RunCoroutine(Bleed());
                    BleedEffect.Instance.Coroutines.Add(co);
                }
            }
        }

        public void OnMedicalItemUsed(UsedMedicalItemEventArgs ev)
        {
            if (beenShot == null || bleeding == null) return;
            if (ev.Item == ItemType.Adrenaline && !BleedEffect.Instance.Config.AdrenalineStopsBleeding) return;
            if (ev.Item == ItemType.Painkillers && !BleedEffect.Instance.Config.PainkillersStopBleeding) return;
            if (ev.Item == ItemType.Medkit && !BleedEffect.Instance.Config.MedKitStopsBleeding) return;
            if (ev.Item == ItemType.SCP500 && !BleedEffect.Instance.Config.SCP500StopsBleeding) return;
            if (beenShot.ContainsKey(ev.Player.Id)) beenShot.Remove(ev.Player.Id);
            if (bleeding.ContainsKey(ev.Player.Id)) bleeding.Remove(ev.Player.Id);
        }

        public void OnDied(DiedEventArgs ev)
        {
            if (beenShot == null || bleeding == null) return;
            if (beenShot.ContainsKey(ev.Target.Id)) beenShot.Remove(ev.Target.Id);
            if (bleeding.ContainsKey(ev.Target.Id)) bleeding.Remove(ev.Target.Id);
        }

        public void OnLeft(LeftEventArgs ev)
        {
            if (beenShot == null || bleeding == null) return;
            if (beenShot.ContainsKey(ev.Player.Id)) beenShot.Remove(ev.Player.Id);
            if (bleeding.ContainsKey(ev.Player.Id)) bleeding.Remove(ev.Player.Id);
        }

        public void OnChangingRole(ChangingRoleEventArgs ev)
        {
            if (beenShot == null || bleeding == null) return;
            if (beenShot.ContainsKey(ev.Player.Id)) beenShot.Remove(ev.Player.Id);
            if (bleeding.ContainsKey(ev.Player.Id)) bleeding.Remove(ev.Player.Id);
        }


        public IEnumerator<float> Bleed()
        {
            while (bleeding != null && bleeding.Count > 0)
            {
                double HealthPerSec = BleedEffect.Instance.Config.HealthDrainPerSecond;
                double HealthPerSecInc = BleedEffect.Instance.Config.HealthDrainPerSecondIncrease;
                foreach (var ent in bleeding)
                {
                    double amount = HealthPerSec;
                    if (ent.Value > 1)
                    {
                        if(BleedEffect.Instance.Config.HealthDrainExponential)
                        {
                            double power = ent.Value;
                            if (amount < 1)
                            {
                                power /= BleedEffect.Instance.Config.HealthDrainDivisor;
                            }
                            amount = Math.Pow(HealthPerSec, power);
                        } else
                        {
                            amount += HealthPerSecInc*(ent.Value-1);
                        }
                    }
                    Log.Debug($"Player with id {ent.Key} has drained {amount} health.", BleedEffect.Instance.Config.Debug);
                    EPlayer p = EPlayer.Get(ent.Key);
                    if (p.Health - amount <= 0)
                    {
                        bleeding.Remove(ent.Key);
                        beenShot.Remove(ent.Key);
                        p.Kill(DamageTypes.Bleeding);
                        continue;
                    }
                    p.Health -= (float) amount;
                }
                yield return Timing.WaitForSeconds(1f);
            }
            BleedEffect.Instance.mainCoroEnabled = false;
            Log.Debug($"Stopping Coro {co}", BleedEffect.Instance.Config.Debug);
            BleedEffect.Instance.Coroutines.Remove(co);
            Timing.KillCoroutines(co);
            yield break;
        }
    }
}
