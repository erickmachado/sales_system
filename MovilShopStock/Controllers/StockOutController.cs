﻿using Microsoft.AspNet.Identity;
using MovilShopStock.Models;
using MovilShopStock.Models.Catalog;
using MovilShopStock.Models.Handlers;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace MovilShopStock.Controllers
{
    [Authorize(Roles = RoleConstants.Dealer + "," + RoleConstants.Editor + "," + RoleConstants.Administrator)]
    public class StockOutController : GenericController
    {
        private ApplicationDbContext applicationDbContext = new ApplicationDbContext();

        public async Task<ActionResult> Index()
        {
            Guid business_working = Guid.Parse(Session["BusinessWorking"].ToString());
            List<StockOutModel> result = new List<StockOutModel>();

            List<StockOut> stockOuts = await applicationDbContext.StockOuts.Include("Product").Include("Product.Category").Include("User").Where(x => x.Product.Business_Id == business_working).OrderByDescending(x => x.Date).ToListAsync();

            foreach (var stockOut in stockOuts)
            {
                result.Add(new StockOutModel()
                {
                    Id = stockOut.Id.ToString(),
                    ProductName = stockOut.Product.Name,
                    Date = stockOut.Date.ToString("yyyy-MM-dd"),
                    Quantity = stockOut.Quantity,
                    User = stockOut.User.UserName,
                    SalePrice = stockOut.SalePrice,
                    Gain = stockOut.Gain,
                    Receivered = stockOut.Receiver != null,
                    Receiver = stockOut.Receiver?.UserName,
                    Category = stockOut.Product.Category.Name
                });
            }

            return View(result);
        }

        [HttpGet]
        public async Task<ActionResult> Create()
        {
            StockOutModel model = new StockOutModel();

            ViewBag.Categories = await applicationDbContext.Categories.OrderBy(x => x.Name).ToListAsync();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(StockOutModel model)
        {
            if (ModelState.IsValid)
            {
                Guid productId = Guid.Parse(model.ProductName);

                Product product = await applicationDbContext.Products.Include("Category").FirstOrDefaultAsync(x => x.Id == productId);

                StockOut stockOut = new StockOut()
                {
                    Id = Guid.NewGuid(),
                    Product_Id = productId,
                    Date = DateTime.Now,
                    Quantity = model.Quantity,
                    SalePrice = model.SalePrice,
                    User_Id = User.Identity.GetUserId(),
                    Gain = model.SalePrice - product.CurrentPrice
                };

                if (User.IsInRole(RoleConstants.Editor) || User.IsInRole(RoleConstants.Administrator))
                {
                    stockOut.Receiver_Id = User.Identity.GetUserId();

                    if (!product.NoCountOut)
                    {
                        User user = await applicationDbContext.Users.FirstOrDefaultAsync(x => x.Id == stockOut.Receiver_Id);

                        if (product.Category.ActionOut == ActionConstants.Sum)
                        {
                            user.Cash += stockOut.SalePrice * stockOut.Quantity;
                        }
                        else if (product.Category.ActionOut == ActionConstants.Rest)
                        {
                            user.Cash -= stockOut.SalePrice * stockOut.Quantity;
                        }

                        applicationDbContext.Entry(user).State = System.Data.Entity.EntityState.Modified;
                    }
                }

                applicationDbContext.StockOuts.Add(stockOut);

                product.Out += stockOut.Quantity;
                product.LastUpdated = DateTime.Now;

                applicationDbContext.Entry(product).State = System.Data.Entity.EntityState.Modified;

                await applicationDbContext.SaveChangesAsync();

                return RedirectToAction("Index");
            }
            ViewBag.Categories = await applicationDbContext.Categories.OrderBy(x => x.Name).ToListAsync();
            return View(model);
        }

        [HttpGet]
        [Authorize(Roles = RoleConstants.Editor + "," + RoleConstants.Administrator)]
        public async Task<ActionResult> Receiver(string id)
        {
            Guid out_id = Guid.Parse(id);

            StockOut stockOut = await applicationDbContext.StockOuts.Include("Product").Include("Product.Category").FirstOrDefaultAsync(x => x.Id == out_id);

            if (stockOut != null)
            {
                stockOut.Receiver_Id = User.Identity.GetUserId();

                applicationDbContext.Entry(stockOut).State = System.Data.Entity.EntityState.Modified;

                User user = await applicationDbContext.Users.FirstOrDefaultAsync(x => x.Id == stockOut.Receiver_Id);
                if (!stockOut.Product.NoCountOut)
                {
                    if (stockOut.Product.Category.ActionOut == ActionConstants.Sum)
                    {
                        user.Cash += stockOut.SalePrice * stockOut.Quantity;
                    }
                    else if (stockOut.Product.Category.ActionOut == ActionConstants.Rest)
                    {
                        user.Cash -= stockOut.SalePrice * stockOut.Quantity;
                    }

                    applicationDbContext.Entry(user).State = System.Data.Entity.EntityState.Modified;
                }

                await applicationDbContext.SaveChangesAsync();
            }

            return RedirectToAction("Index");
        }
    }
}