<?php
$titleid=$_GET['id'];
if($_GET['query'] == "TitleID")
{

		$ch = curl_init();
		$options = [
		    CURLOPT_SSL_VERIFYPEER => false,
		    CURLOPT_RETURNTRANSFER => true,
		    CURLOPT_URL            => 'http://xboxunity.net/api/Covers/'.$titleid
		];
		
		curl_setopt_array($ch, $options);
		$data = json_decode(curl_exec($ch));
		curl_close($ch);
		
		cover($data);
}
elseif($_GET['query'] == "SearchTerm")
{
	$ch = curl_init();
	$options = [
	    CURLOPT_SSL_VERIFYPEER => false,
	    CURLOPT_RETURNTRANSFER => true,
	    CURLOPT_URL            => 'http://xboxunity.net/api/Covers/00000000/'.str_replace(",", " ", $titleid)
	];
	
	curl_setopt_array($ch, $options);
	$data = json_decode(curl_exec($ch));
	curl_close($ch);
	
	cover($data);
}

function cover($data)
{
	$xml = new SimpleXMLElement('<?xml version="1.0" encoding="ISO-8859-1"?>'.'<covers/>');
		
	if(count($data)>0)
	{
		foreach ($data as &$value) {		
		    $title = $xml->addChild('title');
		    $title->addAttribute('id',$value->titleid);
		    $title->addAttribute('name',$value->name);
		    $title->addAttribute('mediaid','');
		    $title->addAttribute('filename','Cover.png');
		    $title->addAttribute('type','');
		    $title->addAttribute('mainlink',$value->url);
		    $title->addAttribute('front',$value->front);
		    $title->addAttribute('Official',(int)$value->official);    
		}
	}
	
	Header('Content-type: text/xml');
	print($xml->asXML());
}
?>