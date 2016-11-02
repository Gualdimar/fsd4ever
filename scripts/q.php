<?php
define('INCLUDE_CHECK',true);

$ch = curl_init();
$options = [
    CURLOPT_SSL_VERIFYPEER => false,
    CURLOPT_RETURNTRANSFER => true,
    CURLOPT_URL            => 'http://xboxunity.net/api/q/'.$_GET["pwd"]
];

curl_setopt_array($ch, $options);
$data = json_decode(curl_exec($ch));
curl_close($ch);
	
if(count($data->q)>0)
{
	$xml = new SimpleXMLElement('<?xml version="1.0" encoding="ISO-8859-1"?>'.'<covers/>');
	
	foreach ($data->q as &$value) {
		if($value->type==1)
		{		
		    $titleid = strtoupper(dechex((int)$value->titleid));
		
		    $title = $xml->addChild('title');
		    $title->addAttribute('id',$titleid);
		    $title->addAttribute('name',$value->title);
		    $title->addAttribute('mediaid','');
		    $title->addAttribute('filename','Cover.png');
		    $title->addAttribute('type','');
		    $title->addAttribute('mainlink',$value->url);
		    $title->addAttribute('front',str_replace('boxart','boxartfront',$value->url));
		    $title->addAttribute('Official','0');   
	    	}
	}
	
	Header('Content-type: text/xml');
	print($xml->asXML());
}
?>