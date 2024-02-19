using Firebase.Analytics;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;

public class UnityIAP : MonoBehaviour, IStoreListener
{
    private static IStoreController m_StoreController;
    private static IExtensionProvider m_StoreExtensionProvider;
    public Action onPurchaseComplete;
    public static UnityIAP Instance
    {
        get;
        private set;
    }
    private void Awake()
    {
        if (UnityIAP.Instance != null)
        {
            UnityEngine.Object.DontDestroyOnLoad(base.gameObject);
            return;
        }
        UnityIAP.Instance = this;

    }
    public string[] kProductIDs;
    void Start()
    {
        if (m_StoreController == null)
        {
            InitializePurchasing();
        }
    }
    public void InitializePurchasing()
    {
        if (IsInitialized())
            return;

        Debug.Log("Initializing Purchase...");

        var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
        builder.AddProduct(kProductIDs[0], ProductType.Consumable);                               //Ahmad
        builder.AddProduct(kProductIDs[1], ProductType.NonConsumable);
        builder.AddProduct(kProductIDs[2], ProductType.NonConsumable);
        /*builder.AddProduct(kProductIDs[3], ProductType.NonConsumable);
        builder.AddProduct(kProductIDs[4], ProductType.Consumable);
        builder.AddProduct(kProductIDs[5], ProductType.Consumable);
        builder.AddProduct(kProductIDs[6], ProductType.Consumable);
        builder.AddProduct(kProductIDs[7], ProductType.NonConsumable);
        builder.AddProduct(kProductIDs[8], ProductType.NonConsumable);
        builder.AddProduct(kProductIDs[9], ProductType.NonConsumable);
        builder.AddProduct(kProductIDs[10], ProductType.NonConsumable);
        builder.AddProduct(kProductIDs[11], ProductType.NonConsumable);
        builder.AddProduct(kProductIDs[12], ProductType.NonConsumable);
        builder.AddProduct(kProductIDs[13], ProductType.NonConsumable);
        builder.AddProduct(kProductIDs[14], ProductType.NonConsumable);
        builder.AddProduct(kProductIDs[15], ProductType.NonConsumable);
        builder.AddProduct(kProductIDs[16], ProductType.NonConsumable);*/
        UnityPurchasing.Initialize(this, builder);
    }
    private bool IsInitialized()
    {
        return m_StoreController != null && m_StoreExtensionProvider != null;
    }
    public void GeneralFunctionl(int id)
    {
        BuyProductID(kProductIDs[id]);
    }
    void BuyProductID(string productId)
    {
        if (IsInitialized())
        {
            Product product = m_StoreController.products.WithID(productId);

            if (product != null && product.availableToPurchase)
            {
                m_StoreController.InitiatePurchase(product, "0.99$");
            }
            else
            {
                Debug.Log("BuyProductID: FAIL. Not purchasing product, either is not found or is not available for purchase");
            }
        }
        else
        {
            Debug.Log("BuyProductID FAIL. Not initialized.");
        }
    }
    public void RestorePurchases()
    {
        if (!IsInitialized())
        {
            //GA Event
            FirebaseAnalytics.LogEvent("Restore" + "_Purchase" + "_failed");

            Debug.Log("RestorePurchases FAIL. Not initialized.");
            //return;
        }

        //Functionality for restore purchases
        PlayerPrefs.SetInt("noAds", 0);
        PlayerPrefs.SetInt("levelsCompleted", 0);
        //

        //GA Event
        FirebaseAnalytics.LogEvent("Restore" + "_Purchase" + "_success");

        if (Application.platform == RuntimePlatform.IPhonePlayer ||
             Application.platform == RuntimePlatform.OSXPlayer)
        {
            Debug.Log("RestorePurchases started ...");

            var apple = m_StoreExtensionProvider.GetExtension<IAppleExtensions>();
            apple.RestoreTransactions((result) =>
            {
                Debug.Log("RestorePurchases continuing: " + result + ". If no further messages, no purchases available to restore.");
            });
        }
        else
        {
            Debug.Log("RestorePurchases FAIL. Not supported on this platform. Current = " + Application.platform);
            //GA Event
            FirebaseAnalytics.LogEvent("Restore" + "_Purchase" + "_failed");
        }
    }
    /****** Unity IAP Callback Events ******/
    public void OnInitialized(IStoreController controller, IExtensionProvider extensions)
    {
        Debug.Log("OnInitialized: PASS");

        m_StoreController = controller;
        m_StoreExtensionProvider = extensions;
    }
    public void OnInitializeFailed(InitializationFailureReason error)
    {
        Debug.Log("OnInitializeFailed InitializationFailureReason:" + error);
    }
    public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args)
    {
        if (String.Equals(args.purchasedProduct.definition.id, kProductIDs[0], StringComparison.Ordinal))       //Ahmad
        {
            if (this.onPurchaseComplete != null)
            {
                this.onPurchaseComplete();
                this.onPurchaseComplete = null;
            }
        }
        if (String.Equals(args.purchasedProduct.definition.id, kProductIDs[1], StringComparison.Ordinal))
        {
            if (this.onPurchaseComplete != null)
            {
                this.onPurchaseComplete();
                this.onPurchaseComplete = null;
            }
        }
        if (String.Equals(args.purchasedProduct.definition.id, kProductIDs[2], StringComparison.Ordinal))
        {
            if (this.onPurchaseComplete != null)
            {
                this.onPurchaseComplete();
                this.onPurchaseComplete = null;
            }
        }
        /*if (String.Equals(args.purchasedProduct.definition.id, kProductIDs[3], StringComparison.Ordinal))
        {
            if (this.onPurchaseComplete != null)
            {
                this.onPurchaseComplete();
                this.onPurchaseComplete = null;
            }
        }

        if (String.Equals(args.purchasedProduct.definition.id, kProductIDs[4], StringComparison.Ordinal))
        {
            if (this.onPurchaseComplete != null)
            {
                this.onPurchaseComplete();
                this.onPurchaseComplete = null;
            }
        }
        if (String.Equals(args.purchasedProduct.definition.id, kProductIDs[5], StringComparison.Ordinal))
        {
            if (this.onPurchaseComplete != null)
            {
                this.onPurchaseComplete();
                this.onPurchaseComplete = null;
            }
        }
        if (String.Equals(args.purchasedProduct.definition.id, kProductIDs[6], StringComparison.Ordinal))
        {
            if (this.onPurchaseComplete != null)
            {
                this.onPurchaseComplete();
                this.onPurchaseComplete = null;
            }
        }
        if (String.Equals(args.purchasedProduct.definition.id, kProductIDs[7], StringComparison.Ordinal))
        {
            if (this.onPurchaseComplete != null)
            {
                this.onPurchaseComplete();
                this.onPurchaseComplete = null;
            }
        }
        if (String.Equals(args.purchasedProduct.definition.id, kProductIDs[8], StringComparison.Ordinal))
        {
            if (this.onPurchaseComplete != null)
            {
                this.onPurchaseComplete();
                this.onPurchaseComplete = null;
            }
        }
        if (String.Equals(args.purchasedProduct.definition.id, kProductIDs[9], StringComparison.Ordinal))
        {
            if (this.onPurchaseComplete != null)
            {
                this.onPurchaseComplete();
                this.onPurchaseComplete = null;
            }
        }
        if (String.Equals(args.purchasedProduct.definition.id, kProductIDs[10], StringComparison.Ordinal))
        {
            if (this.onPurchaseComplete != null)
            {
                this.onPurchaseComplete();
                this.onPurchaseComplete = null;
            }
        }
        if (String.Equals(args.purchasedProduct.definition.id, kProductIDs[11], StringComparison.Ordinal))
        {
            if (this.onPurchaseComplete != null)
            {
                this.onPurchaseComplete();
                this.onPurchaseComplete = null;
            }
        }
        if (String.Equals(args.purchasedProduct.definition.id, kProductIDs[12], StringComparison.Ordinal))
        {
            if (this.onPurchaseComplete != null)
            {
                this.onPurchaseComplete();
                this.onPurchaseComplete = null;
            }
        }
        if (String.Equals(args.purchasedProduct.definition.id, kProductIDs[13], StringComparison.Ordinal))
        {
            if (this.onPurchaseComplete != null)
            {
                this.onPurchaseComplete();
                this.onPurchaseComplete = null;
            }
        }
        if (String.Equals(args.purchasedProduct.definition.id, kProductIDs[14], StringComparison.Ordinal))
        {
            if (this.onPurchaseComplete != null)
            {
                this.onPurchaseComplete();
                this.onPurchaseComplete = null;
            }
        }
        if (String.Equals(args.purchasedProduct.definition.id, kProductIDs[15], StringComparison.Ordinal))
        {
            if (this.onPurchaseComplete != null)
            {
                this.onPurchaseComplete();
                this.onPurchaseComplete = null;
            }
        }
        if (String.Equals(args.purchasedProduct.definition.id, kProductIDs[16], StringComparison.Ordinal))
        {
            if (this.onPurchaseComplete != null)
            {
                this.onPurchaseComplete();
                this.onPurchaseComplete = null;
            }
        }*/
        return PurchaseProcessingResult.Complete;
    }
    public void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
    {
        Debug.Log(string.Format("OnPurchaseFailed: FAIL. Product: '{0}', PurchaseFailureReason: {1}", product.definition.storeSpecificId, failureReason));
    }

    public void OnInitializeFailed(InitializationFailureReason error, string message)
    {
        throw new NotImplementedException();
    }
    /****** Unity IAP Callback Events ******/

    public void Coins5000(int inAppProductIdIndex)
    {
        onPurchaseComplete = delegate
        {
            economyManager.Instance.addMoney(5000000);
            //GA Event
            FirebaseAnalytics.LogEvent("IAP_5000Coins" + "_Bought");
        };
        GeneralFunctionl(inAppProductIdIndex);
    }

    public void NoAds(int inAppProductIdIndex)
    {
        onPurchaseComplete = delegate
        {
            PlayerPrefs.SetInt("noAds", 1);
            AdsManager.Instance.HideBanner();
            AdsManager.Instance.HideMRecAd();
            //GA Event
            FirebaseAnalytics.LogEvent("IAP_NoAds" + "_Bought");
        };
        GeneralFunctionl(inAppProductIdIndex);
    }

    public void premiumBundle(int inAppProductIdIndex)
    {
        onPurchaseComplete = delegate
        {
            economyManager.Instance.addMoney(5000000);

            
            PlayerPrefs.SetInt("noAds", 1);
            PlayerPrefs.SetInt("levelsCompleted", 10);

            //GA Event
            FirebaseAnalytics.LogEvent("IAP_PremiumBundle" + "_Bought");

            AdsManager.Instance.HideBanner();
            AdsManager.Instance.HideMRecAd();
        };
        GeneralFunctionl(inAppProductIdIndex);
    }
}