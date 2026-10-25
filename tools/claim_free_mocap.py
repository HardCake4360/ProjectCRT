"""Normal zero-price Gumroad checkout, using the user's explicitly supplied receipt email."""
from pathlib import Path
import argparse,hashlib,html,http.cookiejar,json,re,urllib.request,urllib.error,uuid,zipfile
ROOT=Path(__file__).resolve().parents[1];LOG=ROOT/'Logs/RebeccaAnimationAssets'
def main():
    parser=argparse.ArgumentParser();parser.add_argument('--receipt-email',required=True);args=parser.parse_args()
    opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()));opener.addheaders=[('User-Agent','Mozilla/5.0')]
    records=[]
    for number in [4,2]:
        slug='mocap_pack'+str(number);result_path=LOG/(slug+'-claim-result.json')
        if result_path.exists():print(slug,'Previous checkout result exists; not creating another order.');continue
        url='https://andersonrohr.gumroad.com/l/'+slug
        page=opener.open(url,timeout=40).read().decode();data=json.loads(html.unescape(re.search(r'data-page="([^"]+)"',page,re.S)[1]));product=data['props']['product']
        assert product['price_cents']==0 and not product['is_physical'] and not product['is_recurring_billing'],'Only an explicitly free one-time digital product is permitted'
        # No card details, payment token, account creation or mailing-list subscription is supplied.
        token=data['props'].get('authenticity_token');uid=str(uuid.uuid4())
        item={'uid':uid,'permalink':product['permalink'],'quantity':1,'price_cents':'0','perceived_price_cents':'0','tip_cents':'0','is_preorder':False,'is_rental':False,'is_multi_buy':False,'variants':[],'custom_fields':[],'bundle_products':[],'referrer':url,'url_parameters':{},'is_purchasing_power_parity_discounted':False,'pay_in_installments':False,'force_new_subscription':False}
        payload={'email':args.receipt_email,'purchase':{},'line_items':[item],'locale':'en','is_gift':False,'save_card':False,'authenticity_token':token}
        request=urllib.request.Request('https://gumroad.com/orders',data=json.dumps(payload).encode(),headers={'Content-Type':'application/json','Accept':'application/json','X-Requested-With':'XMLHttpRequest','X-CSRF-Token':token or '','Origin':'https://gumroad.com','Referer':url})
        try:
            response=json.load(opener.open(request,timeout=60));result={'http_status':200,'response':response}
        except urllib.error.HTTPError as error:
            body=error.read().decode();result={'http_status':error.code,'error':body[:1200]}
        # Receipt data stays in the ignored local Logs directory and is never copied to asset credits.
        result_path.write_text(json.dumps(result,indent=2),encoding='utf-8')
        response=result.get('response',{});line=response.get('line_items',{}).get(uid,{})
        print(slug,'HTTP',result['http_status'],'success',line.get('success'),'message',line.get('error_message') or response.get('error_message') or response.get('error'),flush=True)
        if result['http_status']!=200 or not line.get('success'):continue
        content=line.get('content_url');assert content and content.startswith('https://'),'No normal download page returned'
        listing=opener.open(content,timeout=40).read().decode();(LOG/(slug+'-download-page.html')).write_text(listing,encoding='utf-8');print(slug,'Receipt and content page received.',flush=True)
if __name__=='__main__':main()
