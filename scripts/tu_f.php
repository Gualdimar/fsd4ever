<?php
$ch = curl_init();
$options = [
    CURLOPT_SSL_VERIFYPEER => false,
    CURLOPT_RETURNTRANSFER => true,
    CURLOPT_URL            => 'http://xboxunity.net/api/tu/'.$_GET["tid"]
];

curl_setopt_array($ch, $options);
$data = json_decode(curl_exec($ch));
curl_close($ch);

$xml = new SimpleXMLElement('<?xml version="1.0" encoding="ISO-8859-1"?>'.'<updates/>');

if(count($data)>0)
{
    foreach ($data as &$value) {
	if(strtoupper($_GET["mid"])==strtoupper($value->mediaid))
        {
	        $title = $xml->addChild('title');
	        $title->addAttribute('id',$value->titleid);
	        $title->addAttribute('updatename',$value->displayname);
	        $title->addAttribute('filename',$value->filename);
	        $title->addAttribute('mediaid',$value->mediaid);
	        $title->addAttribute('link',$value->url);
	        $title->addAttribute('hash',strtolower($value->tuhash));
	        $title->addAttribute('release','');
	        $title->addAttribute('version',$value->version);
	        $title->addAttribute('filesize',$value->filesize);
	}
    }
}

Header('Content-type: text/xml');
print($xml->asXML());

?>