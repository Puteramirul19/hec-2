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
        /// </summary>
        /// <returns>TariffBlock with Complex Billing Structure</returns>
        public ActionResult ReadTariff()
        {
            // Keep existing energy tariff blocks
            var list = db.Tariffs.OrderBy(x => x.Sequence).ToList();
            var count = list.Count();

            // New billing components as per July 2025 update
            var billingComponents = new
            {
                // Energy tariff (existing structure)
                energyTiers = list.Take(count - 1).Select(x => new { boundary = x.BoundryTier, rate = x.TariffPerKWh }),
                energyRemaining = list[count - 1].TariffPerKWh,

                // Additional billing components (as per Excel)
                components = new
                {
                    afa = new { rate = 0.0000, threshold = 600, description = "Automated Fuel Cost Adjustment (AFA)" },
                    capacity = new { rate = 0.0329, threshold = 600, description = "Capacity Charge" },
                    network = new { rate = 0.0963, threshold = 600, description = "Network Charge" },
                    retail = new { fixedRate = 10.00, threshold = 600, description = "Retail Charge (RM/month)" },
                    eei = new { rate = -0.0300, maxKwh = 1000, description = "Energy Efficiency Incentive" },
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