using Hec.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Data.Entity;
using System.Data.Entity.SqlServer;
using Microsoft.Ajax.Utilities;

namespace Hec.Web.Areas.Public.Controllers
{
    public class TipsList
    {
        public Guid Id { get; set; }
        public string ApplianceName { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public int DoneCount { get; set; }
        public UserTipStatus? Status { get; set; }
        public decimal Watt { get; set; }
    }

    public class Top5Appliance
    {
        public string category { get; set; }
        public decimal value { get; set; }
    }

    public class UsageCalculatorController : Web.Controllers.BaseController
    {
        public UsageCalculatorController(HecContext db) : base(db)
        {
        }

        public ActionResult Index()
        {
            ViewBag.HouseCategories = db.HouseCategories.OrderBy(x => x.Sequence).ToList();
            ViewBag.HouseTypes = db.HouseTypes.OrderBy(x => x.Sequence).ToList();
            ViewBag.Appliances = db.Appliances.ToList();
            ViewBag.AccountNo = "";

            return View();
        }

        public ActionResult Account(string ca)
        {
            ViewBag.HouseCategories = db.HouseCategories.OrderBy(x => x.Sequence).ToList();
            ViewBag.HouseTypes = db.HouseTypes.ToList();
            ViewBag.Appliances = db.Appliances.ToList();
            ViewBag.AccountNo = ca;

            return View("Index");
        }

        /// <summary>
        /// Get House data from Contract Account
        /// </summary>
        /// <param name="id">id is ContractAccount.AccountNo</param>
        /// <returns>House data</returns>
        public async Task<ActionResult> ReadHouseForAccountNo(string userId, string accountNo)
        {
            var ca = await db.ContractAccounts.FirstOrDefaultAsync(x => x.UserId == userId && x.AccountNo == accountNo);
            if (ca == null)
                throw new Exception($"No house data found for User ID '{userId}' and Account No '{accountNo}'");

            return Content(ca.HouseData, "application/json");
            //return Json(ca.House);
        }

        /// <summary>
        /// Save House data into Contract Account
        /// </summary>
        /// <param name="id">id is ContractAccount.AccountNo</param>
        /// <param name="house">House data</param>
        /// <returns>Nothing</returns>
        public async Task<ActionResult> UpdateHouseForAccountNo(string userId, string accountNo, House house)
        {
            var ca = await db.ContractAccounts.FirstOrDefaultAsync(x => x.UserId == userId && x.AccountNo == accountNo);
            if (ca == null)
                throw new Exception($"No house data found for User ID '{userId}' and Account No '{accountNo}'");

            ca.House = house;
            ca.SerializeData();
            await db.SaveChangesAsync();

            return Json("");
        }

        /// <summary>
        /// Get random usage energy tips
        /// </summary>
        /// <param name="house">house is houseData</param>
        /// <returns>Energy tips</returns>
        public async Task<ActionResult> ReadEnergyTips(House house, List<Top5Appliance> top5appliance)
        {
            // Get random appliance tips
            List<TipsList> energyTips = new List<TipsList>();
            if(top5appliance == null)
            {
                return Json(energyTips); ;
            }
            foreach (var appl in top5appliance)
            {
                var app = await db.Appliances.Where(x => x.Name == appl.category).FirstOrDefaultAsync();
                var tip = db.Tips.Where(t => t.TipCategoryId == app.TipCategoryId).OrderBy(x => Guid.NewGuid()).FirstOrDefault();

                if (tip != null)
                {
                    if (User.Identity.IsAuthenticated)
                    {
                        var user = GetCurrentUser();
                        var userTip = db.UserTips.Where(ut => ut.TipId == tip.Id && ut.UserId == user.Id).FirstOrDefault();
                        energyTips.Add(new TipsList()
                        {
                            Id = tip.Id,
                            ApplianceName = app.Name,
                            Title = tip.Title,
                            Description = tip.Description,
                            DoneCount = tip.DoneCount,
                            Status = (userTip == null) ? (UserTipStatus?)null : userTip.Status,
                            Watt = appl.value
                        });
                    }
                    else
                    {
                        energyTips.Add(new TipsList()
                        {
                            Id = tip.Id,
                            ApplianceName = app.Name,
                            Title = tip.Title,
                            Description = tip.Description,
                            DoneCount = tip.DoneCount,
                            Status = (UserTipStatus?)null,
                            Watt = appl.value
                        });
                    }
                }
            }

            // Sort by highest watt
            var usageTips = energyTips.OrderByDescending(o => o.Watt).ToList();

            return Json(usageTips);
        }


        /// <summary>
        /// Read Tariff Block with Complex Billing Components (Updated July 2025)
        /// Includes Tiered EEI Structure
        /// </summary>
        /// <returns>TariffBlock with Complex Billing Structure and Tiered EEI</returns>
        public ActionResult ReadTariff()
        {
            // Keep existing energy tariff blocks
            var list = db.Tariffs.OrderBy(x => x.Sequence).ToList();
            var count = list.Count();

            var energyTiers = list.Take(count - 1).Select(x => new { boundary = x.BoundryTier, rate = x.TariffPerKWh });
            var energyRemaining = list[count - 1].TariffPerKWh;

            // Return both old and new formats for compatibility
            var billingComponents = new
            {
                // OLD FORMAT (for backward compatibility)
                tiers = energyTiers,
                remaining = energyRemaining,

                // NEW FORMAT (for new complex billing)
                energyTiers = energyTiers,
                energyRemaining = energyRemaining,

                // Additional billing components (as per Excel)
                components = new
                {
                    afa = new { rate = 0.0000, threshold = 600, description = "Automated Fuel Cost Adjustment (AFA)" },
                    capacity = new { rate = 0.0329, threshold = 600, description = "Capacity Charge" },
                    network = new { rate = 0.0963, threshold = 600, description = "Network Charge" },
                    retail = new { fixedRate = 10.00, threshold = 600, description = "Retail Charge (RM/month)" },

                    // CORRECTED: Tiered EEI structure instead of fixed rate
                    eeiTiers = new[] {
                new { maxKwh = 200, rate = 0.25, description = "EEI Tier 1: 0-200 kWh at -0.25 RM/kWh" },
                new { maxKwh = 250, rate = 0.245, description = "EEI Tier 2: 201-250 kWh at -0.245 RM/kWh" },
                new { maxKwh = 300, rate = 0.225, description = "EEI Tier 3: 251-300 kWh at -0.225 RM/kWh" },
                new { maxKwh = 350, rate = 0.21, description = "EEI Tier 4: 301-350 kWh at -0.21 RM/kWh" },
                new { maxKwh = 400, rate = 0.17, description = "EEI Tier 5: 351-400 kWh at -0.17 RM/kWh" },
                new { maxKwh = 450, rate = 0.145, description = "EEI Tier 6: 401-450 kWh at -0.145 RM/kWh" },
                new { maxKwh = 500, rate = 0.12, description = "EEI Tier 7: 451-500 kWh at -0.12 RM/kWh" },
                new { maxKwh = 550, rate = 0.105, description = "EEI Tier 8: 501-550 kWh at -0.105 RM/kWh" },
                new { maxKwh = 600, rate = 0.09, description = "EEI Tier 9: 551-600 kWh at -0.09 RM/kWh" },
                new { maxKwh = 650, rate = 0.075, description = "EEI Tier 10: 601-650 kWh at -0.075 RM/kWh" },
                new { maxKwh = 700, rate = 0.055, description = "EEI Tier 11: 651-700 kWh at -0.055 RM/kWh" },
                new { maxKwh = 750, rate = 0.045, description = "EEI Tier 12: 701-750 kWh at -0.045 RM/kWh" },
                new { maxKwh = 800, rate = 0.04, description = "EEI Tier 13: 751-800 kWh at -0.04 RM/kWh" },
                new { maxKwh = 850, rate = 0.025, description = "EEI Tier 14: 801-850 kWh at -0.025 RM/kWh" },
                new { maxKwh = 900, rate = 0.01, description = "EEI Tier 15: 851-900 kWh at -0.01 RM/kWh" },
                new { maxKwh = 1000, rate = 0.005, description = "EEI Tier 16: 901-1000 kWh at -0.005 RM/kWh" }
            },

                    serviceTax = new { rate = 0.08, threshold = 600, description = "Service Tax (8%)" },
                    reFund = new { rate = 0.016, threshold = 300, description = "RE Fund (KWTBB 1.6%)" },
                    rebate = new { rate = 0.10, description = "Rebate (10%)" }
                }
            };

            return Json(billingComponents);
        }

        /// <summary>
        /// Read House Type
        /// </summary>
        /// <returns>PremiseType</returns>
        public ActionResult GetHouseType(string houseType)
        {
            var houseTypes = db.HouseTypes.Where(x => x.HouseTypeCode == houseType).FirstOrDefault();
            var houseCategories = db.HouseCategories.Where(x => x.Id == houseTypes.HouseCategoryId).FirstOrDefault();

            return Json(new { houseTypes = houseTypes, houseCategories = houseCategories });
        }
    }
}